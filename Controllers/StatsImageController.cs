using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;

namespace EasyFortniteStats_ImageApi.Controllers;

public class StatsImageController : ControllerBase
{
    private readonly IMemoryCache _cache;
    
    public StatsImageController(IMemoryCache cache)
    {
        _cache = cache;
    }
    
    [HttpPost("stats")]
    public IActionResult Post([FromBody] Stats stats, String type = "normal")
    {
        if (!type.Equals("normal") && !type.Equals("competitive")) return BadRequest("Invalid type");
        
        var templateMutex = _cache.Get<Mutex>($"stats_{type}_template_mutex");
        templateMutex.WaitOne();
        
        _cache.TryGetValue($"stats_{type}_template_image", out SKBitmap? templateBitmap);
        if (templateBitmap == null)
        {
            templateBitmap = GenerateTemplate(stats, type);
            _cache.Set($"stats_{type}_template_image", templateBitmap);
        }
        templateMutex.ReleaseMutex();
        
        using var bitmap = GenerateImage(stats, type, templateBitmap);
        using SKImage image = SKImage.FromBitmap(bitmap);
        SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return File(data.AsStream(true), "image/png");
    }

    private SKBitmap GenerateTemplate(Stats stats, string type)
    {
        SKImageInfo imageInfo;
        imageInfo = type.Equals("competitive") ? new SKImageInfo(1505, 624) : new SKImageInfo(1505, 777);
        
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear();
        
        var fullBackgroundImagePath = $"data/images/shop/{stats.BackgroundImagePath}";
        if (stats.BackgroundImagePath == null || !System.IO.File.Exists(fullBackgroundImagePath))
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
            using var stream = System.IO.File.OpenRead(fullBackgroundImagePath);
            var customBackgroundBitmap = SKBitmap.Decode(stream);
            if (customBackgroundBitmap.Width != imageInfo.Width || customBackgroundBitmap.Height != imageInfo.Height)
            {
                customBackgroundBitmap = customBackgroundBitmap.Resize(imageInfo, SKFilterQuality.High);
            }
            // TODO: Round image corners
            canvas.DrawBitmap(customBackgroundBitmap, 0, 0);
        }

        using (var nameSplit = new SKPaint())
        {
            nameSplit.IsAntialias = true;
            nameSplit.Color = SKColors.Gray;
            
            canvas.DrawRoundRect(134, 57, 5, 50, 3, 3, nameSplit);
        }
        
        using var boxPaint = new SKPaint();
        boxPaint.IsAntialias = true;
        boxPaint.Color = SKColors.White.WithAlpha((int) (.2 * 255));
        
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
            var overallBoxRect = new SKRoundRect(SKRect.Create(50, 159, 437, 415), 30);
            DrawBlurredRoundRect(bitmap, overallBoxRect);
            canvas.DrawRoundRect(overallBoxRect, boxPaint);
            
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
            var overallBoxRect = new SKRoundRect(SKRect.Create(50, 159, 437, 568), 30);
            DrawBlurredRoundRect(bitmap, overallBoxRect);
            canvas.DrawRoundRect(overallBoxRect, boxPaint);
            
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
            
            titlePaint.MeasureText("BattlePass Level", ref textBounds);
            canvas.DrawText("BattlePass Level", 70, 442 - textBounds.Top, titlePaint);
            
            var battlePassBarBackgroundPaint = new SKPaint();
            battlePassBarBackgroundPaint.IsAntialias = true;
            battlePassBarBackgroundPaint.Color = SKColors.White.WithAlpha((int)(.3 * 255));
            canvas.DrawRoundRect(158, 483, 309, 20, 10, 10, battlePassBarBackgroundPaint);
        }
        
        // Solo
        var soloBoxRect = new SKRoundRect(SKRect.Create(517, 159, 459, 185), 30);
        DrawBlurredRoundRect(bitmap, soloBoxRect);
        canvas.DrawRoundRect(soloBoxRect, boxPaint);
        
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
        var duosBoxRect = new SKRoundRect(SKRect.Create(996, 159, 459, 185), 30);
        DrawBlurredRoundRect(bitmap, duosBoxRect);
        canvas.DrawRoundRect(duosBoxRect, boxPaint);
        
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
        var triosBoxRect = new SKRoundRect(SKRect.Create(517, 389, 459, 185), 30);
        DrawBlurredRoundRect(bitmap, triosBoxRect);
        canvas.DrawRoundRect(triosBoxRect, boxPaint);
        
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
        var squadsBoxRect = new SKRoundRect(SKRect.Create(996, 389, 459, 185), 30);
        DrawBlurredRoundRect(bitmap, squadsBoxRect);
        canvas.DrawRoundRect(squadsBoxRect, boxPaint);
        
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
            var teamsBoxRect = new SKRoundRect(SKRect.Create(517, 619, 938, 108), 30);
            DrawBlurredRoundRect(bitmap, teamsBoxRect);
            canvas.DrawRoundRect(teamsBoxRect, boxPaint);
        
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
        using var canvas = new SKCanvas(template);
        
        using var fortniteFont = SKTypeface.FromFile(@"Assets/Fonts/Fortnite.ttf");
        using var segoeFont = SKTypeface.FromFile(@"Assets/Fonts/Segoe.ttf");
        
        using var namePaint = new SKPaint();
        namePaint.IsAntialias = true;
        namePaint.Color = SKColors.White;
        namePaint.Typeface = segoeFont;
        namePaint.TextSize = 64;
        
        using var titlePaint = new SKPaint();
        titlePaint.IsAntialias = true;
        titlePaint.Color = SKColors.LightGray;
        titlePaint.Typeface = segoeFont;
        titlePaint.TextSize = 20;

        using var valuePaint = new SKPaint();
        valuePaint.IsAntialias = true;
        valuePaint.Color = SKColors.White;
        valuePaint.Typeface = fortniteFont;
        valuePaint.TextSize = 35;
        
        using var divisionPaint = new SKPaint();
        divisionPaint.IsAntialias = true;
        divisionPaint.Color = SKColors.White;
        divisionPaint.Typeface = fortniteFont;
        divisionPaint.TextSize = 29;
        
        var textBounds = new SKRect();
        
        using var inputIcon = SKBitmap.Decode($"Assets/Images/Stats/InputTypes/{stats.InputType}.png");
        canvas.DrawBitmap(inputIcon, 50, 50);
        
        namePaint.MeasureText(stats.PlayerName, ref textBounds);
        canvas.DrawText(stats.PlayerName, 159, 58 - textBounds.Top, namePaint);
        
        if (stats.IsVerified)
        {
            using var verifiedIcon = SKBitmap.Decode("Assets/Images/Stats/Verified.png");
            canvas.DrawBitmap(verifiedIcon, 159 + textBounds.Width + 5, 47);

            var discordBoxBitmap = GenerateDiscordBox(stats.UserName ?? "???#0000");
            canvas.DrawBitmap(discordBoxBitmap, template.Width - 50 - discordBoxBitmap.Width, 39);
        }

        if (type.Equals("competitive") && stats.Arena != null)
        {
            valuePaint.MeasureText(stats.Arena.HypePoints, ref textBounds);
            canvas.DrawText(stats.Arena.HypePoints, 70, 189 - textBounds.Top, valuePaint);
            
            using var divisionIconBitmap = SKBitmap.Decode(
                $"Assets/Images/Stats/DivisionIcons/{Regex.Match(stats.Arena.Division, @"\d+").Value}.png");
            canvas.DrawBitmap(divisionIconBitmap, 219, 139);
            
            divisionPaint.MeasureText(stats.Arena.Division, ref textBounds);
            canvas.DrawText(stats.Arena.Division, 326, 189 - textBounds.Top, divisionPaint);
            
            titlePaint.MeasureText(stats.Arena.League, ref textBounds);
            canvas.DrawText(stats.Arena.League, 326, 215 - textBounds.Top, titlePaint);
            
            valuePaint.MeasureText(stats.Arena.Earnings, ref textBounds);
            canvas.DrawText(stats.Arena.Earnings, 70, 319 - textBounds.Top, valuePaint);

            valuePaint.MeasureText(stats.Arena.PowerRanking, ref textBounds);
            canvas.DrawText(stats.Arena.PowerRanking, 250, 319 - textBounds.Top, valuePaint);

            valuePaint.MeasureText(stats.Overall.MatchesPlayed, ref textBounds);
            canvas.DrawText(stats.Overall.MatchesPlayed, 70, 396 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Overall.Wins, ref textBounds);
            canvas.DrawText(stats.Overall.Wins, 231, 396 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Overall.WinRatio, ref textBounds);
            canvas.DrawText(stats.Overall.WinRatio, 370, 396 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Overall.Kills, ref textBounds);
            canvas.DrawText(stats.Overall.Kills, 70, 473 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Overall.KD, ref textBounds);
            canvas.DrawText(stats.Overall.KD, 231, 473 - textBounds.Top, valuePaint);
        }
        else
        {
            valuePaint.MeasureText(stats.Overall.MatchesPlayed, ref textBounds);
            canvas.DrawText(stats.Overall.MatchesPlayed, 70, 211 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Overall.Wins, ref textBounds);
            canvas.DrawText(stats.Overall.Wins, 231, 211 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Overall.WinRatio, ref textBounds);
            canvas.DrawText(stats.Overall.WinRatio, 370, 211 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Overall.Kills, ref textBounds);
            canvas.DrawText(stats.Overall.Kills, 70, 288 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Overall.KD, ref textBounds);
            canvas.DrawText(stats.Overall.KD, 231, 288 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Playtime.Days, ref textBounds);
            canvas.DrawText(stats.Playtime.Days, 70, 369 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Playtime.Hours, ref textBounds);
            canvas.DrawText(stats.Playtime.Hours, 147, 369 - textBounds.Top, valuePaint);
            
            valuePaint.MeasureText(stats.Playtime.Minutes, ref textBounds);
            canvas.DrawText(stats.Playtime.Minutes, 213, 369 - textBounds.Top, valuePaint);

            var battlePassLevel = ((int)stats.BattlePassLevel).ToString();
            valuePaint.MeasureText(battlePassLevel, ref textBounds);
            canvas.DrawText(battlePassLevel, 70, 479 - textBounds.Top, valuePaint);

            
            var battlePassBarWidth = (int)(309 * (stats.BattlePassLevel - (int)stats.BattlePassLevel));
            if (battlePassBarWidth > 0)
            {
                battlePassBarWidth = battlePassBarWidth < 20 ? 20 : battlePassBarWidth;
                using var battlePassBarPaint = new SKPaint();
                battlePassBarPaint.IsAntialias = true;
                battlePassBarPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(158, 0),
                    new SKPoint(158 + battlePassBarWidth, 0),
                    new[] { SKColor.Parse(stats.BattlePassLevelBarColors[0]), SKColor.Parse(stats.BattlePassLevelBarColors[1])},
                    new float[] {0, 1},
                    SKShaderTileMode.Repeat);
                
                canvas.DrawRoundRect(158, 483, battlePassBarWidth, 20, 10, 10, battlePassBarPaint);
            }
            
        }
        
        valuePaint.MeasureText(stats.Solo.MatchesPlayed, ref textBounds);
        canvas.DrawText(stats.Solo.MatchesPlayed, 537, 211 - textBounds.Top, valuePaint);

        valuePaint.MeasureText(stats.Solo.Wins, ref textBounds);
        canvas.DrawText(stats.Solo.Wins, 698, 211 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Solo.WinRatio, ref textBounds);
        canvas.DrawText(stats.Solo.WinRatio, 837, 211 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Solo.Kills, ref textBounds);
        canvas.DrawText(stats.Solo.Kills, 537, 288 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Solo.KD, ref textBounds);
        canvas.DrawText(stats.Solo.KD, 698, 288 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Solo.Top25, ref textBounds);
        canvas.DrawText(stats.Solo.Top25, 837, 288 - textBounds.Top, valuePaint);
        
        
        valuePaint.MeasureText(stats.Duos.MatchesPlayed, ref textBounds);
        canvas.DrawText(stats.Duos.MatchesPlayed, 1016, 211 - textBounds.Top, valuePaint);

        valuePaint.MeasureText(stats.Duos.Wins, ref textBounds);
        canvas.DrawText(stats.Duos.Wins, 1177, 211 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Duos.WinRatio, ref textBounds);
        canvas.DrawText(stats.Duos.WinRatio, 1316, 211 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Duos.Kills, ref textBounds);
        canvas.DrawText(stats.Duos.Kills, 1016, 288 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Duos.KD, ref textBounds);
        canvas.DrawText(stats.Duos.KD, 1177, 288 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Duos.Top12, ref textBounds);
        canvas.DrawText(stats.Duos.Top12, 1316, 288 - textBounds.Top, valuePaint);

        
        valuePaint.MeasureText(stats.Trios.MatchesPlayed, ref textBounds);
        canvas.DrawText(stats.Trios.MatchesPlayed, 537, 441 - textBounds.Top, valuePaint);

        valuePaint.MeasureText(stats.Trios.Wins, ref textBounds);
        canvas.DrawText(stats.Trios.Wins, 698, 441 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Trios.WinRatio, ref textBounds);
        canvas.DrawText(stats.Trios.WinRatio, 837, 441 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Trios.Kills, ref textBounds);
        canvas.DrawText(stats.Trios.Kills, 537, 518 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Trios.KD, ref textBounds);
        canvas.DrawText(stats.Trios.KD, 698, 518 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Trios.Top6, ref textBounds);
        canvas.DrawText(stats.Trios.Top6, 837, 518 - textBounds.Top, valuePaint);

        
        valuePaint.MeasureText(stats.Squads.MatchesPlayed, ref textBounds);
        canvas.DrawText(stats.Squads.MatchesPlayed, 1016, 441 - textBounds.Top, valuePaint);

        valuePaint.MeasureText(stats.Squads.Wins, ref textBounds);
        canvas.DrawText(stats.Squads.Wins, 1177, 441 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Squads.WinRatio, ref textBounds);
        canvas.DrawText(stats.Squads.WinRatio, 1316, 441 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Squads.Kills, ref textBounds);
        canvas.DrawText(stats.Squads.Kills, 1016, 518 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Squads.KD, ref textBounds);
        canvas.DrawText(stats.Squads.KD, 1177, 518 - textBounds.Top, valuePaint);
        
        valuePaint.MeasureText(stats.Squads.Top6, ref textBounds);
        canvas.DrawText(stats.Squads.Top6, 1316, 518 - textBounds.Top, valuePaint);

        if (type.Equals("normal") && stats.Teams != null)
        {
            valuePaint.MeasureText(stats.Teams.MatchesPlayed, ref textBounds);
            canvas.DrawText(stats.Teams.MatchesPlayed, 537, 671 - textBounds.Top, valuePaint);

            valuePaint.MeasureText(stats.Teams.Wins, ref textBounds);
            canvas.DrawText(stats.Teams.Wins, 698, 671 - textBounds.Top, valuePaint);
        
            valuePaint.MeasureText(stats.Teams.WinRatio, ref textBounds);
            canvas.DrawText(stats.Teams.WinRatio, 837, 671 - textBounds.Top, valuePaint);
        
            valuePaint.MeasureText(stats.Teams.Kills, ref textBounds);
            canvas.DrawText(stats.Teams.Kills, 954, 671 - textBounds.Top, valuePaint);
        
            valuePaint.MeasureText(stats.Teams.KD, ref textBounds);
            canvas.DrawText(stats.Teams.KD, 1115, 671 - textBounds.Top, valuePaint);
        }
        
        return template;
    }

    private SKBitmap GenerateDiscordBox(string username)
    {
        using var segoeFont = SKTypeface.FromFile(@"Assets/Fonts/Segoe.ttf");

        using var discordTagTextPaint = new SKPaint();
        discordTagTextPaint.IsAntialias = true;
        discordTagTextPaint.Color = SKColors.White;
        discordTagTextPaint.Typeface = segoeFont;
        discordTagTextPaint.TextSize = 25;
        
        var discordTagTextBounds = new SKRect();
        discordTagTextPaint.MeasureText(username, ref discordTagTextBounds);

        var imageInfo = new SKImageInfo(Math.Min((int)discordTagTextBounds.Width + 10 + 2 * 15 + 50, 459), 62);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        using (var discordBoxPaint = new SKPaint())
        {
            discordBoxPaint.IsAntialias = true;
            discordBoxPaint.Color = new SKColor(88, 101, 242);
            
            canvas.DrawRoundRect(0, 0, imageInfo.Width, imageInfo.Height, 15, 15, discordBoxPaint);
        }
        
        using var discordLogoBitmap = SKBitmap.Decode(@"Assets/Images/Stats/DiscordLogo.png");
        canvas.DrawBitmap(discordLogoBitmap, 10,  (float)(imageInfo.Height - discordLogoBitmap.Height) / 2);

        while (discordTagTextBounds.Width + 10 + 2 * 15 + 50 > imageInfo.Width)
        {
            discordTagTextPaint.TextSize--;
            discordTagTextPaint.MeasureText(username, ref discordTagTextBounds);
        }
        
        canvas.DrawText(username, 10 + 15 + discordLogoBitmap.Width, 
            (float)imageInfo.Height / 2 - discordTagTextBounds.MidY, discordTagTextPaint);
        
        return bitmap;
    }
    
    private void DrawBlurredRoundRect(SKBitmap bitmap, SKRoundRect rect)
    {
        using var canvas = new SKCanvas(bitmap);

        canvas.ClipRoundRect(rect, antialias:true);

        using var paint = new SKPaint();
        paint.IsAntialias = true;
        paint.ImageFilter = SKImageFilter.CreateBlur(5, 5);
            
        canvas.DrawBitmap(bitmap, 0, 0, paint);
    }
        
}