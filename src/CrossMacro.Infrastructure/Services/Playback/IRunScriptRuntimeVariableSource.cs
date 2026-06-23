using System.Collections.Generic;

namespace CrossMacro.Infrastructure.Services.Playback;

public interface IRunScriptRuntimeVariableSource
{
    IReadOnlyDictionary<string, string> RuntimeVariables { get; }
}
