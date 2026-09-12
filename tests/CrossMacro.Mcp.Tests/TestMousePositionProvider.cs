namespace CrossMacro.Mcp.Tests;

internal sealed class TestMousePositionProvider : IMousePositionProvider
    {
        public string ProviderName => "test-cursor";

        public bool IsSupported => true;

        public bool SupportsAbsolutePosition => true;

        public (int X, int Y)? Position { get; init; }

        public Task<(int X, int Y)?> GetAbsolutePositionAsync() => Task.FromResult(Position);

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>((1920, 1080));

        public void Dispose()
        {
        }
    }
