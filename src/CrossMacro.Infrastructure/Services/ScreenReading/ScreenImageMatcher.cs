using CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenImageMatcher : IDisposable
{
    private const long MaxTemplateCacheBytes = 64L * 1024 * 1024;

    private readonly PreparedTemplateCache _preparedTemplates = new(MaxTemplateCacheBytes);
    private readonly DeterministicMatchSearch _deterministicSearch;
    private readonly Lock _lifetimeLock = new();

    private long _activeSearchCount;
    private bool _disposeRequested;

    public ScreenImageMatcher()
    {
        _deterministicSearch = new DeterministicMatchSearch(_preparedTemplates);
    }

    public ScreenImageMatch? FindMatch(ScreenFrame frame, ScreenFrame template, ScreenImageMatchOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var searchLease = EnterSearchLease();
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(template);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= ScreenImageMatchOptions.Default;
        ValidateOptions(options);
        EnsureReadable(frame);
        EnsureReadable(template);
        cancellationToken.ThrowIfCancellationRequested();
        var region = options.SearchRegion ?? frame.LogicalBounds;
        if (!frame.LogicalBounds.Contains(region))
        {
            throw new ArgumentOutOfRangeException(nameof(options), region, "The image search region is outside the frame bounds.");
        }

        if (options.SelectionMode is not ScreenImageMatchSelectionMode.Automatic && (template.Width > region.Width || template.Height > region.Height))
        {
            return null;
        }

        if (options.SelectionMode is ScreenImageMatchSelectionMode.Automatic
            || !DeterministicMatchSearch.IsWithinWorkBudget(region, template, options.AnchorPointCount))
        {
            return FindAutomaticMatch(frame, template, region, options, cancellationToken);
        }

        try
        {
            return _deterministicSearch.Find(frame, template, region, options, cancellationToken);
        }
        catch (ScreenImageMatcherResourceLimitException exception) when (!exception.IsPreparationLimit)
        {
            return FindAutomaticMatch(frame, template, region, options, cancellationToken);
        }
    }

    /// <summary>Rejects new searches at once; the cache is released by the last in-flight search when one is running.</summary>
    public void Dispose()
    {
        lock (_lifetimeLock)
        {
            if (_disposeRequested)
            {
                return;
            }

            _disposeRequested = true;
            if (_activeSearchCount > 0)
            {
                return;
            }
        }

        _preparedTemplates.Clear();
    }

    private ScreenImageMatch? FindAutomaticMatch(ScreenFrame frame, ScreenFrame template, ScreenRect region, ScreenImageMatchOptions options, CancellationToken cancellationToken)
    {
        return new AutomaticMatchSearch(_preparedTemplates, frame, template, region, options, cancellationToken).Find();
    }

    private MatcherSearchLease EnterSearchLease()
    {
        lock (_lifetimeLock)
        {
            ObjectDisposedException.ThrowIf(_disposeRequested, this);
            _activeSearchCount++;
            return new MatcherSearchLease(this);
        }
    }

    private void ExitSearchLease()
    {
        bool releaseCaches;
        lock (_lifetimeLock)
        {
            _activeSearchCount--;
            releaseCaches = _disposeRequested && _activeSearchCount == 0;
        }

        if (releaseCaches)
        {
            _preparedTemplates.Clear();
        }
    }

    private static void ValidateOptions(ScreenImageMatchOptions options)
    {
        if (!Enum.IsDefined(options.SelectionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.SelectionMode, "Image match selection mode is invalid.");
        }

        if (!double.IsFinite(options.MinimumSimilarity) || options.MinimumSimilarity is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MinimumSimilarity, "Minimum similarity must be between 0.0 and 1.0.");
        }

        if (options.AnchorPointCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.AnchorPointCount, "Anchor point count cannot be negative.");
        }
    }

    private static void EnsureReadable(ScreenFrame frame)
    {
        _ = frame.TryGetPixel(new ScreenPoint(frame.LogicalBounds.X, frame.LogicalBounds.Y), out _);
    }

    private sealed class MatcherSearchLease(ScreenImageMatcher owner) : IDisposable
    {
        private ScreenImageMatcher? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, value: null)?.ExitSearchLease();
        }
    }
}
