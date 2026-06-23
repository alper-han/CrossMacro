using System.Threading.Tasks;

namespace CrossMacro.UI.Services;

internal interface IPortalScreenReadingGuidanceService
{
    Task ShowBeforePortalWarmupAsync();
}
