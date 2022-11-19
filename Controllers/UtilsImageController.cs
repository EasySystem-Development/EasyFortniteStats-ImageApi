using System.Security.Cryptography;

using EasyFortniteStats_ImageApi.Models;

using Microsoft.AspNetCore.Mvc;

using SkiaSharp;

// ReSharper disable InconsistentNaming
namespace EasyFortniteStats_ImageApi.Controllers;

[ApiController]
[Route("utils")]
public class UtilsImageController : ControllerBase
{
    private readonly SharedAssets _assets;

    public UtilsImageController(SharedAssets assets)
    {
        _assets = assets;
    }

    [HttpPost("seasonProgress")]
    public async Task<IActionResult> GenerateSeasonProgressBar(ProgressBar progressBar)
    {
        using var bitmap = new SKBitmap(568, 30);
        using var canvas = new SKCanvas(bitmap);

        using var barBackgroundPaint = new SKPaint();
        barBackgroundPaint.IsAntialias = true;
        barBackgroundPaint.Color = SKColors.White.WithAlpha((int) (.3 * 255));

        canvas.DrawRoundRect(0, bitmap.Height / 2 - 20 / 2, 500, 20, 10, 10, barBackgroundPaint);

        var barWidth = (int)(500 * progressBar.Progress);
        if (barWidth > 0)
        {
            barWidth = barWidth < 20 ? 20 : barWidth;
            using var barPaint = new SKPaint();
            barPaint.IsAntialias = true;
            barPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(barWidth, 0),
                new[] { SKColor.Parse(progressBar.GradientColors[0]), SKColor.Parse(progressBar.GradientColors[1])},
                new float[] {0, 1},
                SKShaderTileMode.Repeat);

            canvas.DrawRoundRect(0, (float)(bitmap.Height - 20) / 2, barWidth, 20, 10, 10, barPaint);
        }

        var fortniteFont = await _assets.GetFont("Assets/Fonts/Segoe.ttf");

        using var barTextPaint = new SKPaint();
        barTextPaint.IsAntialias = true;
        barTextPaint.Color = SKColors.White;
        barTextPaint.TextSize = 20;
        barTextPaint.Typeface = fortniteFont;

        var barTextBounds = new SKRect();
        barTextPaint.MeasureText(progressBar.Percentage, ref barTextBounds);

        canvas.DrawText(progressBar.Percentage, 505, (float)bitmap.Height / 2 - barTextBounds.MidY, barTextPaint);

        var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return File(data.AsStream(true), "image/png");
    }

    [HttpPost("drop")]
    public async Task<IActionResult> GenerateDropImage(Drop drop)
    {
        var bitmap = await _assets.GetBitmap($"data/images/map_{drop.Locale}.png"); // don't dispose
        using var canvas = new SKCanvas(bitmap);

        var markerAmount = Directory.EnumerateFiles("Assets/Images/Map/Markers", "*.png").Count();
        var markerBitmap = await _assets.GetBitmap($"Assets/Images/Map/Markers/{RandomNumberGenerator.GetInt32(markerAmount - 1)}.png");

        const int worldRadius = 135000;
        var mx = (drop.Y + worldRadius) / (worldRadius * 2) * bitmap.Width;
        var my = (1 - (drop.X + worldRadius) / (worldRadius * 2)) * bitmap.Height;

        canvas.DrawBitmap(markerBitmap, mx - markerBitmap!.Width / 2, my - markerBitmap.Height);

        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
        return File(data.AsStream(true), "image/jpeg");
    }
}
