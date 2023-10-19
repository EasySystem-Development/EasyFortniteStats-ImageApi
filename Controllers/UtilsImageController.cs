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

    [HttpGet("collectGarbage")]
    public IActionResult CollectGarbage()
    {
        GC.Collect();
        return NoContent();
    }

    [HttpPost("progressBar")]
    public async Task<IActionResult> GenerateProgressBar(ProgressBar progressBar)
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

        var segoeFont = await _assets.GetFont("Assets/Fonts/Segoe.ttf");
        var textBounds = new SKRect();

        using var textPaint = new SKPaint();
        textPaint.IsAntialias = true;
        textPaint.Color = SKColors.White;
        textPaint.TextSize = 20;
        textPaint.Typeface = segoeFont;

        textPaint.MeasureText(progressBar.Text, ref textBounds);
        canvas.DrawText(progressBar.Text, 500 + 5, (float)bitmap.Height / 2 - textBounds.MidY, textPaint);

        if (progressBar.BarText != null)
        {
            using var barTextPaint = new SKPaint();
            barTextPaint.IsAntialias = true;
            barTextPaint.Color = SKColors.White;
            barTextPaint.TextSize = 15;
            barTextPaint.Typeface = segoeFont;

            barTextPaint.MeasureText(progressBar.BarText, ref textBounds);
            canvas.DrawText(progressBar.BarText,  (int) ((500 - textBounds.Width) / 2), (float)bitmap.Height / 2 - textBounds.MidY, barTextPaint);
        }

        var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return File(data.AsStream(true), "image/png");
    }

    [HttpPost("drop")]
    public async Task<IActionResult> GenerateDropImage(Drop drop)
    {
        var mapBitmap = await _assets.GetBitmap($"data/images/map/{drop.Locale}.png"); // don't dispose TODO: Clear caching on bg change

        if (mapBitmap == null)
            return BadRequest("Map file doesn't exist.");

        var bitmap = new SKBitmap(mapBitmap.Width, mapBitmap.Height);
        using var canvas = new SKCanvas(bitmap);

        canvas.DrawBitmap(mapBitmap, 0, 0);

        var markerAmount = Directory.EnumerateFiles("Assets/Images/Map/Markers", "*.png").Count();
        var markerBitmap = await _assets.GetBitmap($"Assets/Images/Map/Markers/{RandomNumberGenerator.GetInt32(markerAmount - 1)}.png");  // don't dispose

        const int worldRadius = 150000;
        const int xOffset = 80;
        const int yOffset = 60;

        var mx = ((float)drop.Y + worldRadius) / (worldRadius * 2) * bitmap.Width + xOffset;
        var my = (1 - ((float)drop.X + worldRadius) / (worldRadius * 2)) * bitmap.Height + yOffset;

        canvas.DrawBitmap(markerBitmap, mx - (float)markerBitmap!.Width / 2, my - markerBitmap.Height);

        var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
        return File(data.AsStream(true), "image/jpeg");
    }
}
