using System;

namespace CrossMacro.Cli.Services;

public sealed record ProfileData(string Id, string Name, DateTime CreatedAt, bool IsActive);
