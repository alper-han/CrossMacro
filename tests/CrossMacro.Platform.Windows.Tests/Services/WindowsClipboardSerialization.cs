namespace CrossMacro.Platform.Windows.Tests.Services;

#pragma warning disable CA1515 // xUnit discovers collection definitions by public type.
[CollectionDefinition(nameof(WindowsClipboardSerialization), DisableParallelization = true)]
public sealed class WindowsClipboardSerialization;
#pragma warning restore CA1515
