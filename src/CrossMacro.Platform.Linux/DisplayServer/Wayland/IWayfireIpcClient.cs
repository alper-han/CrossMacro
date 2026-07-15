using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Linux.Services;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal interface IWayfireIpcClient : IDisposable
{
    bool IsAvailable { get; }
    string? SocketPath { get; }
    Task<string?> SendRequestAsync(string method, CancellationToken cancellationToken = default);
}
