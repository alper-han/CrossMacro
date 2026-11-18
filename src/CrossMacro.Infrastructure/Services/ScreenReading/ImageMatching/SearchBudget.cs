using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatcherWork;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

internal sealed class SearchBudget(long maximumWork, long maximumPreparationWork = MaxMatcherPreparationWork)
{
    private long _consumedSearchWork;
    private long _consumedPreparationWork;

    public bool CanConsume(long work)
    {
        if (work <= 0)
        {
            return true;
        }

        var consumed = Volatile.Read(ref _consumedSearchWork);
        return work <= maximumWork - consumed;
    }

    public void Consume(long work)
    {
        if (work <= 0)
        {
            return;
        }

        while (true)
        {
            var consumed = Volatile.Read(ref _consumedSearchWork);
            if (work > maximumWork - consumed)
            {
                var requested = work > long.MaxValue - consumed ? long.MaxValue : consumed + work;
                throw new ScreenImageMatcherResourceLimitException(requested, maximumWork, $"Image matching exceeded the maximum work budget of {maximumWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons.");
            }

            if (Interlocked.CompareExchange(ref _consumedSearchWork, consumed + work, consumed) == consumed)
            {
                return;
            }
        }
    }

    public void ConsumePreparation(long work)
    {
        if (work <= 0)
        {
            return;
        }

        var consumed = Interlocked.Add(ref _consumedPreparationWork, work);
        if (consumed <= maximumPreparationWork)
        {
            return;
        }

        throw new ScreenImageMatcherResourceLimitException(consumed, maximumPreparationWork, $"Image matching preparation exceeded the maximum preparation budget of {maximumPreparationWork.ToString("N0", CultureInfo.InvariantCulture)} channel operations.")
        {
            IsPreparationLimit = true,
        };
    }
}
