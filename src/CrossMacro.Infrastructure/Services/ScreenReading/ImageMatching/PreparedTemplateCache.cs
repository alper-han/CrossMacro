using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatcherWork;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>
/// Prepares templates for matching and caches the results. Owns preparation serialization, LRU order and
/// the complete retained-byte budget.
/// </summary>
internal sealed class PreparedTemplateCache(long maximumBytes)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<TemplateCacheKey, LinkedListNode<TemplateCacheEntry>> _entries = new(TemplateCacheKeyComparer.Instance);
    private readonly LinkedList<TemplateCacheEntry> _recency = new();
    private long _retainedBytes;

    public PreparedTemplate GetPrepared(ScreenFrame template, bool useAlphaMask, byte alphaThreshold, SearchBudget budget, CancellationToken cancellationToken, TemplateCacheContent? cacheContent = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = cacheContent ?? TemplateCacheContent.Create(template, budget, cancellationToken);
        var key = CreateKey(template, useAlphaMask, alphaThreshold, content, cancellationToken);
        return GetOrAdd(key, () => Prepare(template, key, useAlphaMask, alphaThreshold, budget, cancellationToken), cancellationToken);
    }

    public PreparedTemplate GetScaled(ScreenFrame template, RgbImage source, TemplateCacheContent cacheContent, int width, int height, double scale, bool useAlphaMask, byte alphaThreshold, SearchBudget budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = CreateKey(template, useAlphaMask, alphaThreshold, cacheContent, cancellationToken, (int)Math.Round(scale * 1000, MidpointRounding.AwayFromZero)) with
        {
            Width = width,
            Height = height,
        };
        return GetOrAdd(key, () =>
        {
            var useAreaResampling = width < source.Width || height < source.Height;
            budget.ConsumePreparation(EstimateResamplingWork(width, height, sourcePixelCountPerOutput: useAreaResampling ? 9 : 4));
            var scaled = useAreaResampling ? ImageResampling.ResizeArea(source, width, height, cancellationToken) : ImageResampling.ResizeLinear(source, width, height, cancellationToken);
            return new PreparedTemplate(key, scaled, TemplateStatistics.Create(scaled));
        }, cancellationToken);
    }

    public IReadOnlyList<TemplatePyramidLevel> GetPyramid(PreparedTemplate prepared, SearchBudget budget, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_entries.TryGetValue(prepared.Key, out var cached))
            {
                Touch(cached);
                if (cached.Value.Pyramid is { } existing)
                {
                    return existing;
                }
            }

            var pyramid = ImageResampling.BuildTemplatePyramid(prepared, budget, cancellationToken);
            var additionalBytes = pyramid.Skip(1).Sum(static level => FrameNormalization.GetRgbImageByteCount(level.Image));
            if (cached is not null && additionalBytes > 0)
            {
                EvictToFit(additionalBytes, cached);
                if (_retainedBytes + additionalBytes <= maximumBytes)
                {
                    cached.Value.Pyramid = pyramid;
                    cached.Value.SizeBytes = checked(cached.Value.SizeBytes + additionalBytes);
                    _retainedBytes += additionalBytes;
                }
            }

            return pyramid;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _recency.Clear();
            _retainedBytes = 0;
        }
    }

    private static TemplateCacheKey CreateKey(ScreenFrame template, bool useAlphaMask, byte alphaThreshold, TemplateCacheContent content, CancellationToken cancellationToken, int scaleKey = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new TemplateCacheKey(template.Width, template.Height, template.PixelFormat, template.AlphaMode, useAlphaMask, alphaThreshold, scaleKey, content);
    }

    private static PreparedTemplate Prepare(ScreenFrame template, TemplateCacheKey key, bool useAlphaMask, byte alphaThreshold, SearchBudget budget, CancellationToken cancellationToken)
    {
        budget.ConsumePreparation(EstimatePixelWork(template.Width, template.Height, includeAlpha: useAlphaMask));
        var normalized = FrameNormalization.NormalizeFrame(template, useAlphaMask, alphaThreshold, cancellationToken);
        if (normalized.EffectivePixelCount is 0)
        {
            throw new ArgumentException("The template does not contain any pixels above its alpha threshold.", nameof(template));
        }

        var statistics = TemplateStatistics.Create(normalized);
        return new PreparedTemplate(key, normalized, statistics);
    }

    private PreparedTemplate GetOrAdd(TemplateCacheKey key, Func<PreparedTemplate> prepare, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_entries.TryGetValue(key, out var existing))
            {
                Touch(existing);
                return new PreparedTemplate(key, existing.Value.Image, existing.Value.Statistics);
            }

            var prepared = prepare();
            var bytes = checked((long)key.Content.Bytes.Length + FrameNormalization.GetRgbImageByteCount(prepared.Image));
            if (bytes <= maximumBytes)
            {
                EvictToFit(bytes);
                var entry = new TemplateCacheEntry(key, prepared.Image, prepared.Statistics, bytes);
                _entries.Add(key, _recency.AddFirst(entry));
                _retainedBytes += bytes;
            }

            return prepared;
        }
    }

    private void Touch(LinkedListNode<TemplateCacheEntry> node)
    {
        _recency.Remove(node);
        _recency.AddFirst(node);
    }

    private void EvictToFit(long additionalBytes, LinkedListNode<TemplateCacheEntry>? retained = null)
    {
        while (_retainedBytes + additionalBytes > maximumBytes
            && _recency.Last is { } oldest
            && !ReferenceEquals(oldest, retained))
        {
            _recency.Remove(oldest);
            _ = _entries.Remove(oldest.Value.Key);
            _retainedBytes -= oldest.Value.SizeBytes;
        }
    }

    private sealed class TemplateCacheEntry(TemplateCacheKey key, RgbImage image, TemplateStatistics statistics, long sizeBytes)
    {
        public TemplateCacheKey Key { get; } = key;
        public RgbImage Image { get; } = image;
        public TemplateStatistics Statistics { get; } = statistics;
        public IReadOnlyList<TemplatePyramidLevel>? Pyramid { get; set; }
        public long SizeBytes { get; set; } = sizeBytes;
    }
}
