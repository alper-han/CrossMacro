using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Application.Automation;

public interface IManageShortcut
{
    Task<TaskCollectionResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken = default);
    Task<ShortcutTask> AddAsync(ShortcutTask task, CancellationToken cancellationToken = default);
    Task<ShortcutTask> UpdateAsync(ShortcutTask task, CancellationToken cancellationToken = default);
    Task<ShortcutTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task<ShortcutTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default);
}
