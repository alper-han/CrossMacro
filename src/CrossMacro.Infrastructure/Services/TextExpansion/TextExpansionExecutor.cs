using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.TextExpansion;
using Serilog;

namespace CrossMacro.Infrastructure.Services.TextExpansion
{
    public class TextExpansionExecutor : ITextExpansionExecutor
    {
        private const int ClipboardRestoreDelayMs = 1000;
        private const int ClipboardRestoreTimeoutMs = 400;

        private readonly IClipboardService _clipboardService;
        private readonly IKeyboardLayoutService _layoutService;
        private readonly Func<IInputSimulator> _inputSimulatorFactory;

        

        private IInputSimulator? _inputSimulator;

        public TextExpansionExecutor(
            IClipboardService clipboardService,
            IKeyboardLayoutService layoutService,
            Func<IInputSimulator> inputSimulatorFactory)
        {
            _clipboardService = clipboardService;
            _layoutService = layoutService;
            _inputSimulatorFactory = inputSimulatorFactory;
        }

        private IInputSimulator ConvertSimulator()
        {
             if (_inputSimulator == null)
            {
                _inputSimulator = _inputSimulatorFactory();
                _inputSimulator.Initialize(0, 0);
            }
            return _inputSimulator;
        }

        public async Task ExpandAsync(Core.Models.TextExpansion expansion)
        {
             try
            {
                var inputSim = ConvertSimulator();

                // 0. Wait for Modifiers to be released (Safety) - Handled by Coordinator


                // 1. Backspace the trigger
                Log.Debug("Backspacing {Length} chars", expansion.Trigger.Length);
                for (int i = 0; i < expansion.Trigger.Length; i++)
                {
                    await SendKeyAsync(inputSim, 14); // Backspace
                }

                // 2. Insert Replacement
                bool clipboardSuccess = false;

                if (_clipboardService.IsSupported)
                {
                     try 
                    {
                        // Backup clipboard
                        string? oldClipboard = null;
                        try 
                        {
                            var getTask = _clipboardService.GetTextAsync();
                            if (await Task.WhenAny(getTask, Task.Delay(100)) == getTask)
                            {
                                oldClipboard = await getTask;
                            }
                        }
                        catch (Exception ex) { Log.Warning(ex, "Failed to backup clipboard"); }
                        
                        // Set new text
                        var setsTask = _clipboardService.SetTextAsync(expansion.Replacement);
                        if (await Task.WhenAny(setsTask, Task.Delay(100)) == setsTask)
                        {
                            await setsTask; 
                            await Task.Delay(50); 
                            await Task.Delay(50); 
                            
                            // Perform Paste
                            switch (expansion.Method)
                            {
                                case PasteMethod.CtrlShiftV:
                                    await SendKeyAsync(inputSim, 47, shift: true, altGr: false, ctrl: true); 
                                    break;
                                case PasteMethod.ShiftInsert:
                                    await SendKeyAsync(inputSim, 110, shift: true, altGr: false, ctrl: false);
                                    break;
                                case PasteMethod.CtrlV:
                                default:
                                    await SendKeyAsync(inputSim, 47, shift: false, altGr: false, ctrl: true);
                                    break;
                            }
                            
                            await Task.Delay(150);
                            clipboardSuccess = true;
                            
                            // Restore clipboard
                            if (oldClipboard != null)
                            {
                                _ = RestoreClipboardAsync(oldClipboard, expansion.Replacement);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Clipboard paste operation failed");
                    }
                }

                if (!clipboardSuccess)
                {
                    // Fallback: Type directly
                    await TypeTextFallbackAsync(inputSim, expansion.Replacement);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing expansion");
            }
        }

        private async Task TypeTextFallbackAsync(IInputSimulator inputSim, string text)
        {
            Log.Information("Typing replacement directly fallback (length={Length})", text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r') continue;
                if (c == '\n')
                {
                    await SendKeyAsync(inputSim, 28);
                    await Task.Delay(5);
                    continue;
                }

                int codePoint = c;
                bool isSurrogatePair = false;

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    codePoint = char.ConvertToUtf32(text, i);
                    isSurrogatePair = true;
                }

                // Check keyboard layout first
                bool handled = false;
                if (!isSurrogatePair)
                {
                    var input = _layoutService.GetInputForChar(c);
                    if (input.HasValue)
                    {
                        await SendKeyAsync(inputSim, input.Value.KeyCode, input.Value.Shift, input.Value.AltGr);
                        handled = true;
                    }
                }

                if (!handled)
                {
                    await TypeUnicodeHexAsync(inputSim, codePoint);
                }

                if (isSurrogatePair) i++;
                await Task.Delay(1);
            }
        }

        private async Task TypeUnicodeHexAsync(IInputSimulator inputSim, int codePoint)
        {
            // Ctrl+Shift+U sequence
            inputSim.KeyPress(29, true); // Ctrl
            inputSim.Sync(); await Task.Delay(10);
            inputSim.KeyPress(42, true); // Shift
            inputSim.Sync(); await Task.Delay(10);
            await SendKeyAsync(inputSim, 22); // 'u'
            
            inputSim.KeyPress(42, false);
            inputSim.Sync(); await Task.Delay(10);
            inputSim.KeyPress(29, false);
            inputSim.Sync(); 
            
            await Task.Delay(200); // Wait for input mode

            string hex = codePoint.ToString("x");
            foreach (char h in hex)
            {
                var input = _layoutService.GetInputForChar(h);
                if (input.HasValue)
                {
                    await SendKeyAsync(inputSim, input.Value.KeyCode, input.Value.Shift, input.Value.AltGr);
                }
                await Task.Delay(5);
            }
            await Task.Delay(20);
            await SendKeyAsync(inputSim, 28); // Enter
        }

        private async Task SendKeyAsync(IInputSimulator sim, int keyCode, bool shift = false, bool altGr = false, bool ctrl = false)
        {
            if (ctrl) { sim.KeyPress(29, true); sim.Sync(); }
            if (shift) { sim.KeyPress(42, true); sim.Sync(); }
            if (altGr) { sim.KeyPress(100, true); sim.Sync(); }

            sim.KeyPress(keyCode, true);
            sim.Sync();
            await Task.Delay(15);
            sim.KeyPress(keyCode, false);
            sim.Sync();

            if (altGr) { sim.KeyPress(100, false); sim.Sync(); }
            if (shift) { sim.KeyPress(42, false); sim.Sync(); }
            if (ctrl) { sim.KeyPress(29, false); sim.Sync(); }
        }

        private async Task RestoreClipboardAsync(string oldClipboard, string insertedText)
        {
            try
            {
                // Delay restore to reduce race conditions where target apps read clipboard slightly after key combo delivery.
                await Task.Delay(ClipboardRestoreDelayMs);
                var currentClipboard = await TryReadClipboardAsync();
                if (currentClipboard != null &&
                    !string.Equals(currentClipboard, insertedText, StringComparison.Ordinal))
                {
                    // Clipboard changed by user or another app in the meantime; don't clobber it.
                    return;
                }

                var restoreTask = _clipboardService.SetTextAsync(oldClipboard);
                await Task.WhenAny(restoreTask, Task.Delay(ClipboardRestoreTimeoutMs));
            }
            catch
            {
                // Clipboard restore is best-effort.
            }
        }

        private async Task<string?> TryReadClipboardAsync()
        {
            try
            {
                var getTask = _clipboardService.GetTextAsync();
                if (await Task.WhenAny(getTask, Task.Delay(ClipboardRestoreTimeoutMs)) == getTask)
                {
                    return await getTask;
                }
            }
            catch
            {
                // Ignore and allow caller to continue with best-effort behavior.
            }

            return null;
        }
    }
}
