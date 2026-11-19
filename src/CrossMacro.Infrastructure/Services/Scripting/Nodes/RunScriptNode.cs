namespace CrossMacro.Infrastructure.Services.Scripting.Nodes;

internal abstract record RunScriptNode(RunScriptStep Source)
{
    /// <summary>The blocks nested directly inside this node, in source order.</summary>
    public virtual IEnumerable<IReadOnlyList<RunScriptNode>> Blocks => [];
}
