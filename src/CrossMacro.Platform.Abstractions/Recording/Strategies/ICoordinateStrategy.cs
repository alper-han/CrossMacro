using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Abstractions;

public interface ICoordinateStrategy : IDisposable
{
    Task InitializeAsync(CancellationToken ct);

    (int X, int Y) ProcessPosition(InputCaptureEventArgs e);
}
