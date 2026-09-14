namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed partial class ScreenImageMatcher
{
    /// <summary>Owns preparation serialization, LRU order and the complete retained-byte budget.</summary>
    private sealed class PreparedTemplateCache(long maximumBytes)
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<TemplateCacheKey, LinkedListNode<TemplateCacheEntry>> _entries = new(TemplateCacheKeyComparer.Instance);
        private readonly LinkedList<TemplateCacheEntry> _recency = new();
        private long _retainedBytes;

        public PreparedTemplate GetOrAdd(TemplateCacheKey key, Func<PreparedTemplate> prepare, CancellationToken cancellationToken)
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
                var bytes = checked((long)key.Content.Bytes.Length + ImagePreparation.GetRgbImageByteCount(prepared.Image));
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

        public IReadOnlyList<RgbImage> GetPyramid(PreparedTemplate prepared, Func<IReadOnlyList<RgbImage>> build, CancellationToken cancellationToken)
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

                var pyramid = build();
                var additionalBytes = pyramid.Skip(1).Sum(static image => ImagePreparation.GetRgbImageByteCount(image));
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
    }
}
