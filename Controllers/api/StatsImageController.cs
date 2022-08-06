using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace EasyFortniteStats_ImageApi.Controllers;

public class StatsImageController : ControllerBase
{
    [HttpPost("stats")]
    public IActionResult Post([FromForm] Stats stats, String type = "normal")
    {
        if (!type.Equals("normal") && !type.Equals("competitive")) return BadRequest("Invalid type");
        
        using var templateBitmap = GenerateTemplate(stats, type);
        
        using SKImage image = SKImage.FromBitmap(templateBitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return File(data.ToArray(), "image/png");
    }

    private SKBitmap GenerateTemplate(Stats stats, String type)
    {
        SKImageInfo imageInfo;
        if (type.Equals("competitive")) imageInfo = new SKImageInfo(1505, 624);
        else imageInfo = new SKImageInfo(1505, 777);
        
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear();

        if (stats.CustomBackground == null)
        {
            using var backgroundPaint = new SKPaint();
            backgroundPaint.IsAntialias = true;
            backgroundPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(imageInfo.Rect.MidX, imageInfo.Rect.MidY),
                (float)Math.Sqrt(Math.Pow(0-imageInfo.Rect.MidX, 2) + Math.Pow(0-imageInfo.Rect.MidY, 2)),
                new SKColor[] { new(41, 165, 224), new(9, 66, 180)},
                SKShaderTileMode.Clamp);
            
            canvas.DrawRoundRect(0, 0, imageInfo.Width, imageInfo.Height, 25, 25, backgroundPaint);
        }
        else
        {
            var customBackgroundBitmap = SKBitmap.Decode(stats.CustomBackground);
            canvas.DrawBitmap(customBackgroundBitmap, 0, 0);
        }

        using (var nameSplit = new SKPaint())
        {
            nameSplit.IsAntialias = true;
            nameSplit.Color = SKColors.Gray;
            
            canvas.DrawRoundRect(134, 57, 5, 50, 3, 3, nameSplit);
        }
        
        using var blurredBoxPaint = new SKPaint();
        blurredBoxPaint.IsAntialias = true;
        blurredBoxPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Inner, 1);
        blurredBoxPaint.Color = SKColors.White.WithAlpha((int) (.2 * 255));
        
        using var fortniteFont = SKTypeface.FromFile(@"Assets/Fonts/Fortnite.ttf");
        using var segoeFont = SKTypeface.FromFile(@"Assets/Fonts/Segoe.ttf");
        
        using var competitiveBoxTitlePaint = new SKPaint();
        competitiveBoxTitlePaint.IsAntialias = true;
        competitiveBoxTitlePaint.Color = SKColors.White;
        competitiveBoxTitlePaint.Typeface = fortniteFont;
        competitiveBoxTitlePaint.TextSize = 30;
        
        using var boxTitlePaint = new SKPaint();
        boxTitlePaint.IsAntialias = true;
        boxTitlePaint.Color = SKColors.White;
        boxTitlePaint.Typeface = fortniteFont;
        boxTitlePaint.TextSize = 50;
        
        using var titlePaint = new SKPaint();
        titlePaint.IsAntialias = true;
        titlePaint.Color = SKColors.LightGray;
        titlePaint.Typeface = segoeFont;
        titlePaint.TextSize = 20;
        
        var textBounds = new SKRect();

        if (type.Equals("competitive"))
        {
            canvas.DrawRoundRect(49, 159, 437, 415, 30, 30, blurredBoxPaint);
            
            using var overlayBoxPaint = new SKPaint();
            overlayBoxPaint.IsAntialias = true;
            overlayBoxPaint.Color = SKColors.White.WithAlpha((int) (.2 * 255)); 
            
            var upperBoxRect = SKRect.Create(49, 159, 437, 108);
            var upperBox = new SKRoundRect(upperBoxRect);
            upperBox.SetRectRadii(upperBoxRect, new SKPoint[] { new(30, 30), new(30, 30), new(0, 0), new(0, 0) });
            canvas.DrawRoundRect(upperBox, overlayBoxPaint);
            
            competitiveBoxTitlePaint.MeasureText("OVERALL", ref textBounds);
            canvas.DrawText("OVERALL", 211, 252 - textBounds.Top, competitiveBoxTitlePaint);
            
            titlePaint.MeasureText("Arena Hype", ref textBounds);
            canvas.DrawText("Arena Hype", 70, 215 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("Earnings", ref textBounds);
            canvas.DrawText("Earnings", 70, 292 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("Power Ranking", ref textBounds);
            canvas.DrawText("Power Ranking", 250, 292 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("Games", ref textBounds);
            canvas.DrawText("Games", 70, 369 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("Wins", ref textBounds);
            canvas.DrawText("Wins", 231, 369 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("Win%", ref textBounds);
            canvas.DrawText("Win%", 370, 369 - textBounds.Top, titlePaint);

            titlePaint.MeasureText("Kills", ref textBounds);
            canvas.DrawText("Kills", 70, 446 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("K/D", ref textBounds);
            canvas.DrawText("K/D", 231, 446 - textBounds.Top, titlePaint);
        }
        else
        {
            canvas.DrawRoundRect(50, 159, 437, 568, 30, 30, blurredBoxPaint);
            
            boxTitlePaint.MeasureText("OVERALL", ref textBounds);
            canvas.DrawText("OVERALL", 60, 134 - textBounds.Top, boxTitlePaint);

            titlePaint.MeasureText("Games", ref textBounds);
            canvas.DrawText("Games", 70, 184 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("Wins", ref textBounds);
            canvas.DrawText("Wins", 231, 184 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("Win%", ref textBounds);
            canvas.DrawText("Win%", 370, 184 - textBounds.Top, titlePaint);

            titlePaint.MeasureText("Kills", ref textBounds);
            canvas.DrawText("Kills", 70, 261 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("K/D", ref textBounds);
            canvas.DrawText("K/D", 231, 261 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("Playtime since Season 7", ref textBounds);
            canvas.DrawText("Playtime since Season 7", 70, 338 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("days", ref textBounds);
            canvas.DrawText("days", 70, 397 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("hours", ref textBounds);
            canvas.DrawText("hours", 147, 397 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("minutes", ref textBounds);
            canvas.DrawText("minutes", 231, 397 - textBounds.Top, titlePaint);
            
            titlePaint.MeasureText("Battlepass Level", ref textBounds);
            canvas.DrawText("Battlepass Level", 70, 442 - textBounds.Top, titlePaint);
            
            var battlePassBarBackgroundPaint = new SKPaint();
            battlePassBarBackgroundPaint.IsAntialias = true;
            battlePassBarBackgroundPaint.Color = SKColors.White.WithAlpha((int)(.3 * 255));
            canvas.DrawRoundRect(158, 483, 309, 20, 10, 10, battlePassBarBackgroundPaint);
        }
        
        // Solo
        canvas.DrawRoundRect(517, 159, 459, 185, 30, 30, blurredBoxPaint);
        
        boxTitlePaint.MeasureText("SOLO", ref textBounds);
        canvas.DrawText("SOLO", 527, 134 - textBounds.Top, boxTitlePaint);
        
        var soloIcon = SKBitmap.Decode(@"Assets/Images/Stats/PlaylistIcons/solo.png");
        canvas.DrawBitmap(soloIcon, new SKPoint(648, 134));
        
        titlePaint.MeasureText("Games", ref textBounds);
        canvas.DrawText("Games", 537, 184 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Wins", ref textBounds);
        canvas.DrawText("Wins", 698, 184 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Win%", ref textBounds);
        canvas.DrawText("Win%", 837, 184 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Kills", ref textBounds);
        canvas.DrawText("Kills", 537, 261 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("K/D", ref textBounds);
        canvas.DrawText("K/D", 698, 261 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Top 25", ref textBounds);
        canvas.DrawText("Top 25", 837, 261 - textBounds.Top, titlePaint);

        // Duos
        canvas.DrawRoundRect(996, 159, 459, 185, 30, 30, blurredBoxPaint);
        
        boxTitlePaint.MeasureText("DUOS", ref textBounds);
        canvas.DrawText("DUOS", 1006, 134 - textBounds.Top, boxTitlePaint);
        
        var duosIcon = SKBitmap.Decode(@"Assets/Images/Stats/PlaylistIcons/duos.png");
        canvas.DrawBitmap(duosIcon, new SKPoint(1133, 134));
        
        titlePaint.MeasureText("Games", ref textBounds);
        canvas.DrawText("Games", 1016, 184 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Wins", ref textBounds);
        canvas.DrawText("Wins", 1177, 184 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Win%", ref textBounds);
        canvas.DrawText("Win%", 1316, 184 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Kills", ref textBounds);
        canvas.DrawText("Kills", 1016, 261 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("K/D", ref textBounds);
        canvas.DrawText("K/D", 1177, 261 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Top 12", ref textBounds);
        canvas.DrawText("Top 12", 1316, 261 - textBounds.Top, titlePaint);
        
        // Trios
        canvas.DrawRoundRect(517, 389, 459, 185, 30, 30, blurredBoxPaint);
        
        boxTitlePaint.MeasureText("TRIOS", ref textBounds);
        canvas.DrawText("TRIOS", 527, 364 - textBounds.Top, boxTitlePaint);
        
        var triosIcon = SKBitmap.Decode(@"Assets/Images/Stats/PlaylistIcons/trios.png");
        canvas.DrawBitmap(triosIcon, new SKPoint(663, 364));
        
        titlePaint.MeasureText("Games", ref textBounds);
        canvas.DrawText("Games", 537, 414 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Wins", ref textBounds);
        canvas.DrawText("Wins", 698, 414 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Win%", ref textBounds);
        canvas.DrawText("Win%", 837, 414 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Kills", ref textBounds);
        canvas.DrawText("Kills", 537, 491 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("K/D", ref textBounds);
        canvas.DrawText("K/D", 698, 491 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Top 6", ref textBounds);
        canvas.DrawText("Top 6", 837, 491 - textBounds.Top, titlePaint);
        
        // Squads
        canvas.DrawRoundRect(996, 389, 459, 185, 30, 30, blurredBoxPaint);
        
        boxTitlePaint.MeasureText("SQUADS", ref textBounds);
        canvas.DrawText("SQUADS", 1006, 364 - textBounds.Top, boxTitlePaint);
        
        var squadsIcon = SKBitmap.Decode(@"Assets/Images/Stats/PlaylistIcons/squads.png");
        canvas.DrawBitmap(squadsIcon, new SKPoint(1191, 364));
        
        titlePaint.MeasureText("Games", ref textBounds);
        canvas.DrawText("Games", 1016, 414 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Wins", ref textBounds);
        canvas.DrawText("Wins", 1177, 414 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Win%", ref textBounds);
        canvas.DrawText("Win%", 1316, 414 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Kills", ref textBounds);
        canvas.DrawText("Kills", 1016, 491 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("K/D", ref textBounds);
        canvas.DrawText("K/D", 1177, 491 - textBounds.Top, titlePaint);
        
        titlePaint.MeasureText("Top 6", ref textBounds);
        canvas.DrawText("Top 6", 1316, 491 - textBounds.Top, titlePaint);

        if (type.Equals("normal"))
        {
            // Teams
            canvas.DrawRoundRect(517, 619, 938, 108, 30, 30, blurredBoxPaint);
        
            boxTitlePaint.MeasureText("TEAMS", ref textBounds);
            canvas.DrawText("TEAMS", 527, 594 - textBounds.Top, boxTitlePaint);
        
            var teamsIcon = SKBitmap.Decode(@"Assets/Images/Stats/PlaylistIcons/teams.png");
            canvas.DrawBitmap(teamsIcon, new SKPoint(683, 594));
        
            titlePaint.MeasureText("Games", ref textBounds);
            canvas.DrawText("Games", 537, 644 - textBounds.Top, titlePaint);
        
            titlePaint.MeasureText("Wins", ref textBounds);
            canvas.DrawText("Wins", 698, 644 - textBounds.Top, titlePaint);
        
            titlePaint.MeasureText("Win%", ref textBounds);
            canvas.DrawText("Win%", 837, 644 - textBounds.Top, titlePaint);
        
            titlePaint.MeasureText("Kills", ref textBounds);
            canvas.DrawText("Kills", 954, 644 - textBounds.Top, titlePaint);
        
            titlePaint.MeasureText("K/D", ref textBounds);
            canvas.DrawText("K/D", 1115, 644 - textBounds.Top, titlePaint);
        }
        
        return bitmap;
    }

    private SKBitmap GenerateImage(Stats stats, string type, SKBitmap template)
    {
        var canvas = new SKCanvas(template);

        return template;
    }
        
}