namespace CrossMacro.Application.Automation;

/// <summary>
/// The per-task rules of one automation task kind. Task management, the command sets and host
/// authorization share these rules; everything else about the kinds is generic.
/// </summary>
public abstract class AutomationTaskKind<TTask>
    where TTask : class
{
    /// <summary>Lower-case kind label used in messages, for example "schedule".</summary>
    public abstract string Name { get; }

    /// <summary>The reason shown when a task that cannot be enabled is asked to be.</summary>
    public abstract string CannotEnableReason { get; }

    /// <summary>The kind label with a leading capital, for example "Schedule".</summary>
    public string Title => field ??= string.Concat(char.ToUpperInvariant(Name[0]).ToString(), Name.AsSpan(1));

    public abstract Guid IdOf(TTask task);

    public abstract string NameOf(TTask task);

    public abstract TTask Copy(TTask task);

    /// <summary>Re-applies the persisted-task invariants to a task about to be committed.</summary>
    public abstract void Normalize(TTask task);

    public abstract bool IsEnabled(TTask task);

    public abstract bool CanBeEnabled(TTask task);

    /// <summary>Applies the requested state; false when a task that cannot be enabled was asked to be, leaving it disabled.</summary>
    public abstract bool TrySetEnabled(TTask task, bool enabled);

    /// <summary>The macro files the task would play; blank entries mean the task plays no macro file.</summary>
    public abstract IEnumerable<string?> MacroPaths(TTask task);

    /// <summary>Rejects tasks whose referenced files cannot be used. Runs before a task is stored or enabled.</summary>
    public virtual void ValidateFiles(TTask task)
    {
        // Most kinds have nothing to validate.
    }
}
