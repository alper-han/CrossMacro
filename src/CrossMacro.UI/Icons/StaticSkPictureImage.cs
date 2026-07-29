
namespace CrossMacro.UI.Icons;

internal sealed class StaticSkPictureImage(SKPicture picture) : IImage
{
    private readonly SKPicture _picture = picture;

    public Size Size { get; } = new Size(picture.CullRect.Width, picture.CullRect.Height);

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0 || destRect.Width <= 0 || destRect.Height <= 0)
        {
            return;
        }

        context.Custom(new DrawPictureOperation(_picture, sourceRect, destRect));
    }

    private sealed class DrawPictureOperation(SKPicture picture, Rect sourceRect, Rect destRect) : ICustomDrawOperation
    {
        private readonly SKPicture _picture = picture;
        private readonly Rect _sourceRect = sourceRect;

        public Rect Bounds { get; } = destRect;

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                return;
            }

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            _ = canvas.Save();
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

        public void Dispose() { /* Empty */ }
    }
}
