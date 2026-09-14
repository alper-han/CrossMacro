namespace CrossMacro.Core.Services.Recording;

public sealed class MacroEventRecordedEventArgs(MacroEvent macroEvent) : EventArgs
{
    public MacroEvent MacroEvent { get; } = macroEvent;
}
