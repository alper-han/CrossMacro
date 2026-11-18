namespace CrossMacro.UI.ViewModels.Editor;

internal static class UiDispatcherExtensions
{
    /// <summary>Runs an action on the UI thread. Unlike the generic overload it never turns an assignment lambda into a value-returning one.</summary>
    internal static Task RunOnUiThreadAsync(this IUiDispatcher dispatcher, Action action) => dispatcher.InvokeAsync(action);
}
