using System;
using System.Threading.Tasks;

namespace CrossMacro.Core.Services;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync();
}
