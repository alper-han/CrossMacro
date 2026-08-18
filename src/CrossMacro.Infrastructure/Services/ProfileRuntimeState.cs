namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Lightweight startup state shared by the profile coordinator and UI.
/// Keeping this separate from <see cref="IProfileManager"/> avoids resolving
/// the full profile graph merely to decide whether a ViewModel should reload.
/// </summary>
public sealed class ProfileRuntimeState : IProfileRuntimeState
{
    private int _initialized;

    public bool IsInitialized => Volatile.Read(ref _initialized) is 1;

    internal void MarkInitialized()
    {
        Volatile.Write(ref _initialized, 1);
    }
}
