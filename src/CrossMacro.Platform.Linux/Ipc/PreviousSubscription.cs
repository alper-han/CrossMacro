namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>A consumer's subscription before a start request replaced it, so a failed start can restore it.</summary>
internal readonly record struct PreviousSubscription(bool Existed, bool CaptureMouse, bool CaptureKeyboard);
