namespace CrossMacro.Core.Services;

public sealed class MacroEventRecordedEventArgs(MacroEvent macroEvent) : EventArgs
{
    public MacroEvent MacroEvent { get; } = macroEvent;
}
