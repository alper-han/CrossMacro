
namespace CrossMacro.Application.Automation;

public sealed record TaskRequest(Guid? Id = null, bool? Enabled = null, long? ExpectedScopeGeneration = null);
