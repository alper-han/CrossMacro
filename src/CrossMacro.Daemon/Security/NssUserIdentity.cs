namespace CrossMacro.Daemon.Security;

internal readonly record struct NssUserIdentity(string Name, uint PrimaryGroupId);
