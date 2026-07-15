namespace CrossMacro.Core.Services;

public sealed class MacroEventRecordedEventArgs : EventArgs
{
    public MacroEventRecordedEventArgs(MacroEvent macroEvent)
    {
        MacroEvent = macroEvent;
    }

    public MacroEvent MacroEvent { get; }
}
