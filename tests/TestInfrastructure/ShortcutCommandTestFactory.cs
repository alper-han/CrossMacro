using CrossMacro.Application.Automation;
using CrossMacro.Core.Services.Automation.Shortcuts;

namespace CrossMacro.Tests;

internal static class ShortcutCommandTestFactory
{
    public static ShortcutCommands Create(IManageShortcut manageShortcut) =>
        new(manageShortcut, new AcceptingShortcutHotkeyNormalizer());
}
