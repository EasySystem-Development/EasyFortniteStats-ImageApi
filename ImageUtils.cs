using SkiaSharp;

namespace EasyFortniteStats_ImageApi;

public class ImageUtils
{
    public static SKBitmap RotateBitmap(SKBitmap bitmap, float angle)
    {
        var radians = MathF.PI * angle / 180;
        var sine = MathF.Abs(MathF.Sin(radians));
        var cosine = MathF.Abs(MathF.Cos(radians));
        int originalWidth = bitmap.Width, originalHeight = bitmap.Height;
        var rotatedWidth = (int) (cosine * originalWidth + sine * originalHeight);
        var rotatedHeight = (int) (cosine * originalHeight + sine * originalWidth);

        var rotatedBitmap = new SKBitmap(rotatedWidth, rotatedHeight);
        using var rotatedCanvas = new SKCanvas(rotatedBitmap);
        rotatedCanvas.Clear();
        rotatedCanvas.Translate(rotatedWidth / 2f, rotatedHeight / 2f);
        rotatedCanvas.RotateDegrees(-angle);
        rotatedCanvas.Translate(-originalWidth / 2f, -originalHeight / 2f);
        rotatedCanvas.DrawBitmap(bitmap, SKPoint.Empty);

        return rotatedBitmap;
    }
    
    public static SKBitmap GenerateRarityStripe(int width, SKColor rarityColor)
    {
        var imageInfo = new SKImageInfo(width, 14);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        using var paint = new SKPaint();
        {
            paint.IsAntialias = true;
            paint.Color = rarityColor;
            paint.Style = SKPaintStyle.Fill;

            using var path = new SKPath();
            path.MoveTo(0, imageInfo.Height - 5);
            path.LineTo(imageInfo.Width, 0);
            path.LineTo(imageInfo.Width, imageInfo.Height - 6);
            path.LineTo(0, imageInfo.Height);
            path.Close();

            canvas.DrawPath(path, paint);
        }

        return bitmap;
    }
    
    public static SKBitmap GenerateItemCardOverlay(int width, SKBitmap? icon = null)
    {
        var imageInfo = new SKImageInfo(width, 65);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;
            paint.Color = new SKColor(14, 14, 14);
            paint.Style = SKPaintStyle.Fill;

            canvas.DrawRect(0, 0, imageInfo.Width, imageInfo.Height, paint);
        }

        if (icon is not null)
        {
            using var rotatedVbucksBitmap = RotateBitmap(icon, -20);
            using var resizedVBucksBitmap = rotatedVbucksBitmap.Resize(new SKImageInfo(47, 47), SKFilterQuality.Medium);

            canvas.DrawBitmap(resizedVBucksBitmap, new SKPoint(imageInfo.Width - 45, imageInfo.Height - 35));
        }

        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;
            paint.Color = new SKColor(30, 30, 30);
            paint.Style = SKPaintStyle.Fill;

            using var path = new SKPath();
            path.MoveTo(0, imageInfo.Height - 29);
            path.LineTo(imageInfo.Width, imageInfo.Height - 29);
            path.LineTo(imageInfo.Width, imageInfo.Height - 25);
            path.LineTo(0, imageInfo.Height - 24);
            path.Close();

            canvas.DrawPath(path, paint);

            canvas.DrawRect(0, 0, imageInfo.Width, imageInfo.Height - 29, paint);
        }

        return bitmap;
    }
}