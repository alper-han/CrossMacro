using System;
using CrossMacro.Cli.Commands;

namespace CrossMacro.Cli;

public sealed class CliCommandHandlerResolver : ICliCommandHandlerResolver
{
    private readonly Func<MacroValidateCommandHandler> _macroValidateCommandHandler;
    private readonly Func<MacroInfoCommandHandler> _macroInfoCommandHandler;
    private readonly Func<PlayCommandHandler> _playCommandHandler;
    private readonly Func<DoctorCommandHandler> _doctorCommandHandler;
    private readonly Func<SettingsGetCommandHandler> _settingsGetCommandHandler;
    private readonly Func<SettingsSetCommandHandler> _settingsSetCommandHandler;
    private readonly Func<SettingsListKeysCommandHandler> _settingsListKeysCommandHandler;
    private readonly Func<SettingsResetCommandHandler> _settingsResetCommandHandler;
    private readonly Func<ProfileCommandHandler> _profileCommandHandler;
    private readonly Func<TextExpansionCommandHandler> _textExpansionCommandHandler;
    private readonly Func<ScheduleListCommandHandler> _scheduleListCommandHandler;
    private readonly Func<ScheduleRunCommandHandler> _scheduleRunCommandHandler;
    private readonly Func<ShortcutListCommandHandler> _shortcutListCommandHandler;
    private readonly Func<ShortcutRunCommandHandler> _shortcutRunCommandHandler;
    private readonly Func<RecordCommandHandler> _recordCommandHandler;
    private readonly Func<RunCommandHandler> _runCommandHandler;
    private readonly Func<ClipboardCommandHandler> _clipboardCommandHandler;
    private readonly Func<WindowCommandHandler> _windowCommandHandler;
    private readonly Func<ScreenCommandHandler> _screenCommandHandler;
    private readonly Func<ScreenshotCommandHandler> _screenshotCommandHandler;
    private readonly Func<HeadlessCommandHandler> _headlessCommandHandler;

    public CliCommandHandlerResolver(
        Func<MacroValidateCommandHandler> macroValidateCommandHandler,
        Func<MacroInfoCommandHandler> macroInfoCommandHandler,
        Func<PlayCommandHandler> playCommandHandler,
        Func<DoctorCommandHandler> doctorCommandHandler,
        Func<SettingsGetCommandHandler> settingsGetCommandHandler,
        Func<SettingsSetCommandHandler> settingsSetCommandHandler,
        Func<SettingsListKeysCommandHandler> settingsListKeysCommandHandler,
        Func<SettingsResetCommandHandler> settingsResetCommandHandler,
        Func<ProfileCommandHandler> profileCommandHandler,
        Func<TextExpansionCommandHandler> textExpansionCommandHandler,
        Func<ScheduleListCommandHandler> scheduleListCommandHandler,
        Func<ScheduleRunCommandHandler> scheduleRunCommandHandler,
        Func<ShortcutListCommandHandler> shortcutListCommandHandler,
        Func<ShortcutRunCommandHandler> shortcutRunCommandHandler,
        Func<RecordCommandHandler> recordCommandHandler,
        Func<RunCommandHandler> runCommandHandler,
        Func<ClipboardCommandHandler> clipboardCommandHandler,
        Func<WindowCommandHandler> windowCommandHandler,
        Func<ScreenCommandHandler> screenCommandHandler,
        Func<ScreenshotCommandHandler> screenshotCommandHandler,
        Func<HeadlessCommandHandler> headlessCommandHandler)
    {
        _macroValidateCommandHandler = macroValidateCommandHandler;
        _macroInfoCommandHandler = macroInfoCommandHandler;
        _playCommandHandler = playCommandHandler;
        _doctorCommandHandler = doctorCommandHandler;
        _settingsGetCommandHandler = settingsGetCommandHandler;
        _settingsSetCommandHandler = settingsSetCommandHandler;
        _settingsListKeysCommandHandler = settingsListKeysCommandHandler;
        _settingsResetCommandHandler = settingsResetCommandHandler;
        _profileCommandHandler = profileCommandHandler;
        _textExpansionCommandHandler = textExpansionCommandHandler;
        _scheduleListCommandHandler = scheduleListCommandHandler;
        _scheduleRunCommandHandler = scheduleRunCommandHandler;
        _shortcutListCommandHandler = shortcutListCommandHandler;
        _shortcutRunCommandHandler = shortcutRunCommandHandler;
        _recordCommandHandler = recordCommandHandler;
        _runCommandHandler = runCommandHandler;
        _clipboardCommandHandler = clipboardCommandHandler;
        _windowCommandHandler = windowCommandHandler;
        _screenCommandHandler = screenCommandHandler;
        _screenshotCommandHandler = screenshotCommandHandler;
        _headlessCommandHandler = headlessCommandHandler;
    }

    public ICliCommandHandler? Resolve(CliCommandOptions options)
    {
        return options switch
        {
            MacroValidateCliOptions => _macroValidateCommandHandler(),
            MacroInfoCliOptions => _macroInfoCommandHandler(),
            PlayCliOptions => _playCommandHandler(),
            DoctorCliOptions => _doctorCommandHandler(),
            SettingsGetCliOptions => _settingsGetCommandHandler(),
            SettingsSetCliOptions => _settingsSetCommandHandler(),
            SettingsListKeysCliOptions => _settingsListKeysCommandHandler(),
            SettingsResetCliOptions => _settingsResetCommandHandler(),
            ProfileCliOptions => _profileCommandHandler(),
            TextExpansionCliOptions => _textExpansionCommandHandler(),
            ScheduleListCliOptions => _scheduleListCommandHandler(),
            ScheduleRunCliOptions => _scheduleRunCommandHandler(),
            ShortcutListCliOptions => _shortcutListCommandHandler(),
            ShortcutRunCliOptions => _shortcutRunCommandHandler(),
            RecordCliOptions => _recordCommandHandler(),
            RunCliOptions => _runCommandHandler(),
            ClipboardCliOptions => _clipboardCommandHandler(),
            WindowCliOptions => _windowCommandHandler(),
            ScreenCliOptions => _screenCommandHandler(),
            ScreenshotCliOptions => _screenshotCommandHandler(),
            HeadlessCliOptions => _headlessCommandHandler(),
            _ => null
        };
    }
}
