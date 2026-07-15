
namespace CrossMacro.UI.Icons;

internal sealed class StaticSkPictureImage : IImage
{
    private readonly SKPicture _picture;
    private readonly Size _size;

    public StaticSkPictureImage(SKPicture picture)
    {
        _picture = picture;
        _size = new Size(picture.CullRect.Width, picture.CullRect.Height);
    }

    public Size Size => _size;

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0 || destRect.Width <= 0 || destRect.Height <= 0)
        {
            return;
        }

        context.Custom(new DrawPictureOperation(_picture, sourceRect, destRect));
    }

    private sealed class DrawPictureOperation : ICustomDrawOperation
    {
        private readonly SKPicture _picture;
        private readonly Rect _sourceRect;

        public DrawPictureOperation(SKPicture picture, Rect sourceRect, Rect destRect)
        {
            _picture = picture;
            _sourceRect = sourceRect;
            Bounds = destRect;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point point) => Bounds.Contains(point);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                return;
            }

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            try
            {
                canvas.ClipRect(new SKRect((float)Bounds.X, (float)Bounds.Y, (float)Bounds.Right, (float)Bounds.Bottom));
                canvas.Translate((float)Bounds.X, (float)Bounds.Y);
                canvas.Scale((float)(Bounds.Width / _sourceRect.Width), (float)(Bounds.Height / _sourceRect.Height));
                canvas.Translate((float)-_sourceRect.X, (float)-_sourceRect.Y);
                canvas.DrawPicture(_picture);
            }
            finally
            {
                canvas.Restore();
            }
        }

        public void Dispose()
        {
        }
    }
}
