
namespace CrossMacro.Infrastructure.Services.Playback;

public interface IRunScriptRuntimeVariableSource
{
    public IReadOnlyDictionary<string, string> RuntimeVariables { get; }
}
