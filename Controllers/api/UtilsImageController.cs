using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace EasyFortniteStats_ImageApi.Controllers;

public class UtilsImageController : ControllerBase
{

    [HttpPost("utils/seasonProgress")]
    public IActionResult GenerateSeasonProgressBar([FromForm] ProgressBar progressBar)
    {
        using var bitmap = new SKBitmap(568, 30);
        using var canvas = new SKCanvas(bitmap);

        using var barBackgroundPaint = new SKPaint();
        barBackgroundPaint.IsAntialias = true;
        barBackgroundPaint.Color = SKColors.White.WithAlpha((int)(.6 * 255));
        
        canvas.DrawRoundRect(0, bitmap.Height / 2 - 20 / 2, 500, 20, 10, 10, barBackgroundPaint);
        
        var barWidth = (int)(500 * progressBar.Progress);
        if (barWidth > 0)
        {
            barWidth = barWidth < 20 ? 20 : barWidth;
            using var barPaint = new SKPaint();
            barPaint.IsAntialias = true;
            barPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(158, 0),
                new SKPoint(158 + barWidth, 0),
                new[] { SKColor.Parse(progressBar.GradientColors[0]), SKColor.Parse(progressBar.GradientColors[1])},
                new float[] {0, 1},
                SKShaderTileMode.Repeat);
                
            canvas.DrawRoundRect(158, 483, barWidth, 20, 10, 10, barPaint);
        }
        
        using var fortniteFont = SKTypeface.FromFile("Assets/Fonts/Fortnite.ttf");
        
        using var barTextPaint = new SKPaint();
        barTextPaint.IsAntialias = true;
        barTextPaint.Color = SKColors.White;
        barTextPaint.TextSize = 20;
        barTextPaint.Typeface = fortniteFont;
        
        var barTextBounds = new SKRect();
        barTextPaint.MeasureText(progressBar.Percentage, ref barTextBounds);
        
        canvas.DrawText(progressBar.Percentage, 505, bitmap.Height / 2 - barTextBounds.MidY, barBackgroundPaint);
        
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return File(data.ToArray(), "image/png");
    }

    [HttpPost("utils/drop")]
    public IActionResult GenerateDropImage([FromForm] Drop drop)
    {
        using var mapStream = drop.MapImage.OpenReadStream();
        using var bitmap = SKBitmap.Decode(mapStream);
        using var canvas = new SKCanvas(bitmap);


        var markerAmount = new DirectoryInfo("Assets/Images/Map/Markers").GetFiles(".png").Length;
        using var markerStream = new FileStream($"Assets/Images/Map/Markers/{RandomNumberGenerator.GetInt32(markerAmount - 1)}.png", FileMode.Open);
        using var markerBitmap = SKBitmap.Decode(markerStream);

        const int worldRadius = 135000;
        var mx = (drop.Y + worldRadius) / (worldRadius * 2) * bitmap.Width;
        var my = (1 - (drop.X + worldRadius) / (worldRadius * 2)) * bitmap.Height;
        
        canvas.DrawBitmap(markerBitmap, mx - markerBitmap.Width / 2, my - markerBitmap.Height);
        
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        return File(data.ToArray(), "image/jpeg");
    }
}