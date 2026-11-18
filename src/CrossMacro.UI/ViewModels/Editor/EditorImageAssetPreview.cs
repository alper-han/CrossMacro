using System.Runtime.InteropServices;

namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Decodes and exposes the bitmap preview of the image asset used by the selected image action.</summary>
public sealed class EditorImageAssetPreview : ObservableObject
{
    private readonly EditorViewModel _owner;
    private readonly IReadOnlyDictionary<string, string> _imageAssets;
    private readonly IImageAssetPreviewDecoder? _decoder;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly CancellationToken _cancellationToken;
    private int _refreshVersion;

    internal EditorImageAssetPreview(
        EditorViewModel owner,
        IReadOnlyDictionary<string, string> imageAssets,
        IImageAssetPreviewDecoder? decoder,
        IUiDispatcher uiDispatcher,
        CancellationToken cancellationToken)
    {
        _owner = owner;
        _imageAssets = imageAssets;
        _decoder = decoder;
        _uiDispatcher = uiDispatcher;
        _cancellationToken = cancellationToken;
    }

    public WriteableBitmap? Bitmap { get; private set; }

    public bool IsVisible => _owner.Fields.ShowImageSearchFields && Bitmap is not null;

    internal async Task RefreshAsync()
    {
        var refreshVersion = Interlocked.Increment(ref _refreshVersion);
        SetBitmap(bitmap: null);
        if (!_owner.Fields.ShowImageSearchFields)
        {
            return;
        }

        var assetName = _owner.SelectedAction?.ImageAssetName;
        if (string.IsNullOrWhiteSpace(assetName)
            || !_imageAssets.TryGetValue(assetName, out var encoded)
            || string.IsNullOrWhiteSpace(encoded))
        {
            return;
        }

        try
        {
            var decoder = _decoder
                ?? throw new InvalidOperationException("Image asset preview decoder is not registered.");
            var decoded = await decoder.DecodeAsync(encoded, assetName, _cancellationToken).ConfigureAwait(false);

            // Marshal back to the UI thread; the version check drops stale results when the
            // selection changes quickly.
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                if (refreshVersion == Volatile.Read(ref _refreshVersion))
                {
                    SetBitmap(CreateBitmap(decoded));
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when the view model is disposed while a decode is running.
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            await _uiDispatcher.RunOnUiThreadAsync(() => _owner.Status = _owner.LocalizeFormat("Editor_StatusImagePreviewError", ex.Message)).ConfigureAwait(false);
        }
    }

    internal void Clear() => SetBitmap(bitmap: null);

    internal void NotifyChanged() => OnPropertyChanged(string.Empty);

    private void SetBitmap(WriteableBitmap? bitmap)
    {
        if (ReferenceEquals(Bitmap, bitmap))
        {
            return;
        }

        var previous = Bitmap;
        Bitmap = bitmap;
        previous?.Dispose();
        NotifyChanged();
    }

    private static WriteableBitmap CreateBitmap(ImageAssetPreview preview)
    {
        var pixels = preview.Pixels.ToArray();
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            return new WriteableBitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul,
                handle.AddrOfPinnedObject(),
                new PixelSize(preview.Width, preview.Height),
                new Vector(96, 96),
                preview.Stride);
        }
        finally
        {
            handle.Free();
        }
    }
}
