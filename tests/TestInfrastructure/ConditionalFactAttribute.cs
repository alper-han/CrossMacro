using System;
using System.IO;
using Xunit;

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
