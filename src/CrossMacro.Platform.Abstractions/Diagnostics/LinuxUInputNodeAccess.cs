namespace CrossMacro.Platform.Abstractions.Diagnostics;

/// <summary>Whether one uinput device node exists and can be opened for writing.</summary>
public readonly record struct LinuxUInputNodeAccess(bool Exists, bool CanWrite);
