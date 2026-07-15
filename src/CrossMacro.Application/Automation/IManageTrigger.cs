using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Application.Automation;

public interface IManageTrigger
{
    Task<TaskCollectionResult<TriggerTask>> ListAsync(CancellationToken cancellationToken = default);
    Task<TriggerTask> AddAsync(TriggerTask task, CancellationToken cancellationToken = default);
    Task<TriggerTask> UpdateAsync(TriggerTask task, CancellationToken cancellationToken = default);
    Task<TriggerTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task<TriggerTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default);
}
