using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

public interface IImageClickMovementResolver
{
    Task<ImageClickMovementResolution> ResolveAsync(
        IInputSimulator inputSimulator,
        ScreenPoint target,
        CancellationToken cancellationToken);
}
