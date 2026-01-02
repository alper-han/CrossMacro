using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Serialization;
using Serilog;
using CrossMacro.Infrastructure.Helpers;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing and executing shortcut-triggered macros
/// </summary>
public class ShortcutService : IShortcutService
{
    private static readonly ILogger Logger = Log.ForContext<ShortcutService>();
    
    private readonly IMacroFileManager _fileManager;
    private readonly Func<IMacroPlayer> _playerFactory;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly SynchronizationContext? _syncContext;
    private readonly Lock _lock = new();
    private bool _isListening;
    private bool _disposed;
    
    private readonly string _shortcutsFilePath;
    
    // Debounce tracking
    private readonly Dictionary<Guid, DateTime> _lastTriggerTimes = new();
    private const int DebounceIntervalMs = 300;
    
    // Track currently executing tasks and their players for toggle behavior
    private readonly Dictionary<Guid, IMacroPlayer> _activePlayers = new();
    
    public ObservableCollection<ShortcutTask> Tasks { get; } = new();
    public bool IsListening => _isListening;
    
    public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;
    public event EventHandler<ShortcutTask>? ShortcutStarting;
    
    public ShortcutService(
        IMacroFileManager fileManager, 
        Func<IMacroPlayer> playerFactory, 
        IGlobalHotkeyService hotkeyService)
    {
        _fileManager = fileManager;
        _playerFactory = playerFactory;
        _hotkeyService = hotkeyService;
        _syncContext = SynchronizationContext.Current;
        
        _shortcutsFilePath = PathHelper.GetConfigFilePath(ConfigFileNames.Shortcuts);
    }
    
    public void AddTask(ShortcutTask task)
    {
        lock (_lock)
        {
            Tasks.Add(task);
        }
    }
    
    public void RemoveTask(Guid id)
    {
        lock (_lock)
        {
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                Tasks.Remove(task);
            }
        }
    }
    
    public void UpdateTask(ShortcutTask task)
    {
        lock (_lock)
        {
            var existing = Tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing != null)
            {
                existing.Name = task.Name;
                existing.MacroFilePath = task.MacroFilePath;
                existing.HotkeyString = task.HotkeyString;
                existing.PlaybackSpeed = task.PlaybackSpeed;
                existing.IsEnabled = task.IsEnabled;
            }
        }
    }
    
    public void SetTaskEnabled(Guid id, bool enabled)
    {
        lock (_lock)
        {
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.IsEnabled = enabled;
            }
        }
    }
    
    public void Start()
    {
        if (_isListening) return;
        
        _hotkeyService.RawInputReceived += OnRawInputReceived;
        _isListening = true;
        
        Logger.Information("[ShortcutService] Started listening for shortcuts");
    }
    
    public void Stop()
    {
        if (!_isListening) return;
        
        _hotkeyService.RawInputReceived -= OnRawInputReceived;
        _isListening = false;
        
        Logger.Information("[ShortcutService] Stopped listening for shortcuts");
    }
    
    private void OnRawInputReceived(object? sender, RawHotkeyInputEventArgs e)
    {
        ShortcutTask? matchingTask = null;
        IMacroPlayer? playerToStop = null;
        bool shouldStart = false;
        
        lock (_lock)
        {
            // Find matching enabled task
            matchingTask = Tasks.FirstOrDefault(t => 
                t.IsEnabled && 
                string.Equals(t.HotkeyString, e.HotkeyString, StringComparison.OrdinalIgnoreCase));
            
            if (matchingTask == null) return;
            
            // Toggle behavior: if already executing, stop it
            if (_activePlayers.TryGetValue(matchingTask.Id, out playerToStop))
            {
                Logger.Information("[ShortcutService] Stopping {TaskName} - toggle triggered", matchingTask.Name);
                _activePlayers.Remove(matchingTask.Id);
                // Will stop outside lock
            }
            else
            {
                // Debounce check for starting
                var now = DateTime.UtcNow;
                if (_lastTriggerTimes.TryGetValue(matchingTask.Id, out var lastTime))
                {
                    if ((now - lastTime).TotalMilliseconds < DebounceIntervalMs)
                    {
                        return;
                    }
                }
                _lastTriggerTimes[matchingTask.Id] = now;
                shouldStart = true;
            }
        }
        
        // Stop player outside lock
        if (playerToStop != null)
        {
            playerToStop.Stop();
            SafeUpdate(() => matchingTask.LastStatus = "Stopped");
            return;
        }
        
        // Execute asynchronously to not block input handling
        if (shouldStart)
        {
            _ = ExecuteTaskAsync(matchingTask);
        }
    }
    
    private async Task ExecuteTaskAsync(ShortcutTask task)
    {
        IMacroPlayer? player = null;
        try
        {
            if (string.IsNullOrEmpty(task.MacroFilePath) || !File.Exists(task.MacroFilePath))
            {
                SafeUpdate(() => 
                {
                    task.LastStatus = "Macro file not found";
                    task.LastTriggeredTime = DateTime.UtcNow;
                });
                
                ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, false, "Macro file not found"));
                return;
            }
            
            var macro = await _fileManager.LoadAsync(task.MacroFilePath);
            if (macro == null)
            {
                SafeUpdate(() =>
                {
                    task.LastStatus = "Failed to load macro";
                    task.LastTriggeredTime = DateTime.UtcNow;
                });
                ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, false, "Failed to load macro"));
                return;
            }
            
            SafeUpdate(() =>
            {
                task.LastStatus = "Running...";
                task.LastTriggeredTime = DateTime.UtcNow;
            });
            ShortcutStarting?.Invoke(this, task);
            
            player = _playerFactory();
            
            // Register the player so it can be stopped via toggle
            lock (_lock)
            {
                _activePlayers[task.Id] = player;
            }
            
            var options = new PlaybackOptions
            {
                SpeedMultiplier = task.PlaybackSpeed
            };
            
            await player.PlayAsync(macro, options);
            
            SafeUpdate(() =>
            {
                task.LastTriggeredTime = DateTime.UtcNow;
                task.LastStatus = "Success";
            });
            
            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, true, "Executed successfully"));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("progress"))
        {
            SafeUpdate(() =>
            {
                task.LastStatus = "Skipped (playback busy)";
            });
            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, false, "Playback was busy"));
        }
        catch (OperationCanceledException)
        {
            // Playback was stopped/cancelled - this is expected for toggle stop
            SafeUpdate(() =>
            {
                task.LastStatus = "Stopped";
            });
            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, true, "Stopped by user"));
        }
        catch (Exception ex)
        {
            SafeUpdate(() =>
            {
                task.LastStatus = $"Error: {ex.Message}";
                task.LastTriggeredTime = DateTime.UtcNow;
            });
            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, false, ex.Message));
            Logger.Error(ex, "[ShortcutService] Error executing shortcut task {TaskName}", task.Name);
        }
        finally
        {
            // Always cleanup
            lock (_lock)
            {
                _activePlayers.Remove(task.Id);
            }
            player?.Dispose();
        }
    }

    private void SafeUpdate(Action action)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }
    
    public async Task SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_shortcutsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonSerializer.Serialize(Tasks.ToList(), CrossMacroJsonContext.Default.ListShortcutTask);
            await File.WriteAllTextAsync(_shortcutsFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to save shortcut tasks to {Path}", _shortcutsFilePath);
        }
    }
    
    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_shortcutsFilePath)) return;
            
            var json = await File.ReadAllTextAsync(_shortcutsFilePath);
            var tasks = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.ListShortcutTask);
            
            if (tasks != null)
            {
                void UpdateCollection(object? state)
                {
                    lock (_lock)
                    {
                        Tasks.Clear();
                        foreach (var task in tasks)
                        {
                            Tasks.Add(task);
                        }
                    }
                }

                if (_syncContext != null)
                {
                    _syncContext.Post(UpdateCollection, null);
                }
                else
                {
                    UpdateCollection(null);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load shortcut tasks from {Path}", _shortcutsFilePath);
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Stop();
    }
}
