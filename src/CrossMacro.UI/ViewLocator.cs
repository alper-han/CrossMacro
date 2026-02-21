using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CrossMacro.UI.ViewModels;
using CrossMacro.UI.Views.Tabs;

namespace CrossMacro.UI;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        return param switch
        {
            RecordingViewModel => new RecordingTabView(),
            PlaybackViewModel => new PlaybackTabView(),
            FilesViewModel => new FilesTabView(),
            TextExpansionViewModel => new TextExpansionTabView(),
            SettingsViewModel => new SettingsTabView(),
            ScheduleViewModel => new ScheduleTabView(),
            ShortcutViewModel => new ShortcutTabView(),
            EditorViewModel => new EditorTabView(),
            null => null,
            _ => new TextBlock { Text = "Not Found: " + param.GetType().FullName }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
