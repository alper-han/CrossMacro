
namespace CrossMacro.TestInfrastructure;

public abstract class ConditionalFactAttribute : FactAttribute
{
    protected ConditionalFactAttribute(Func<bool> predicate, string requiredEnvironment)
    {
        if (!predicate())
        {
            Skip = $"Requires {requiredEnvironment}.";
        }
    }
}
