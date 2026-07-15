using System;

namespace CrossMacro.Application.Automation;

public sealed record TaskRequest(Guid? Id = null, bool? Enabled = null);
