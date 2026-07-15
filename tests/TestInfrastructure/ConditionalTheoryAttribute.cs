using System;
using Xunit;

namespace CrossMacro.TestInfrastructure;

public abstract class ConditionalTheoryAttribute : TheoryAttribute
{
    protected ConditionalTheoryAttribute(Func<bool> predicate, string requiredEnvironment)
    {
        if (!predicate())
        {
            Skip = $"Requires {requiredEnvironment}.";
        }
    }
}
