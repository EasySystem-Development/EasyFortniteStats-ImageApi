using EasyFortniteStats_ImageApi.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

using SkiaSharp;

// ReSharper disable InconsistentNaming
namespace EasyFortniteStats_ImageApi.Controllers;

[ApiController]
[Route("shop")]
public class ShopImageController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _clientFactory;
    private readonly NamedLock _namedLock;
    private readonly SharedAssets _assets;

    private static readonly MemoryCacheEntryOptions ShopImageCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        PostEvictionCallbacks =
        {
            new PostEvictionCallbackRegistration
            {
                EvictionCallback = ImageUtils.BitmapPostEvictionCallback
            }
        }
    };

    public ShopImageController(IMemoryCache cache, IHttpClientFactory clientFactory, NamedLock namedLock, SharedAssets assets)
    {
        _cache = cache;
        _clientFactory = clientFactory;
        _namedLock = namedLock;
        _assets = assets;
    }

    [HttpPost]
    public async Task<IActionResult> Post(Shop shop)
    {
        // Hash the section ids
        var templateHash = string.Join('-', shop.Sections.Select(x => x.Id)).GetHashCode().ToString();
        var isNewShop = shop.NewShop ?? false;

        SKBitmap? templateBitmap;
        ShopSectionLocationData[]? shopLocationData;

        await _namedLock.WaitAsync("shop_template");
        try
        {
            templateBitmap = _cache.Get<SKBitmap?>($"shop_template_bmp_{templateHash}");
            shopLocationData = _cache.Get<ShopSectionLocationData[]?>($"shop_location_data_{templateHash}");
            if (isNewShop || templateBitmap is null)
            {
                await PrefetchImages(shop);
                var templateGenerationResult = await GenerateTemplate(shop);
                templateBitmap = templateGenerationResult.Item2;
                shopLocationData = templateGenerationResult.Item1;
                _cache.Set($"shop_template_bmp_{templateHash}", templateBitmap, ShopImageCacheOptions);
                _cache.Set($"shop_location_data_{templateHash}", shopLocationData, TimeSpan.FromMinutes(10));
            }
        }
        finally
        {
            _namedLock.Release("shop_template");
        }

        SKBitmap? localeTemplateBitmap;

        var lockName = $"shop_template_{shop.Locale}";
        await _namedLock.WaitAsync(lockName);
        try
        {
            localeTemplateBitmap = _cache.Get<SKBitmap?>($"shop_template_{shop.Locale}_bmp");
            if (isNewShop || localeTemplateBitmap == null)
            {
                localeTemplateBitmap = await GenerateLocaleTemplate(shop, templateBitmap, shopLocationData!);
                _cache.Set($"shop_template_{shop.Locale}_bmp", localeTemplateBitmap, ShopImageCacheOptions);
            }
        }
        finally
        {
            _namedLock.Release(lockName);
        }

        using var localeTemplateBitmapCopy = localeTemplateBitmap.Copy();
        using var shopImage = await GenerateShopImage(shop, localeTemplateBitmapCopy);
        var data = shopImage.Encode(SKEncodedImageFormat.Png, 100);
        return File(data.AsStream(true), "image/png");
    }

    private async Task PrefetchImages(Shop shop)
    {
        var entries = shop.Sections.SelectMany(x => x.Entries);
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount / 2
        };
        await Parallel.ForEachAsync(entries, options, async (entry, token) =>
        {
            var cacheKey = $"shop_image_{entry.Id}";
            var cachedBitmap = _cache.Get<SKBitmap?>(cacheKey);
            if (cachedBitmap is not null)
            {
                entry.Image = cachedBitmap;
                return;
            }

            using var client = _clientFactory.CreateClient();
            var url = entry.ImageUrl ?? entry.FallbackImageUrl;
            //var imageBytes = await client.GetByteArrayAsync(url, token);
            using var imageResponse = await client.GetAsync(url, token);
            await using var imageStream = await imageResponse.Content.ReadAsStreamAsync(token);
            var bitmap = SKBitmap.Decode(imageStream);
            entry.Image = bitmap;
            // cache image for 10 minutes & make sure it gets disposed after the period
            _cache.Set(cacheKey, bitmap, ShopImageCacheOptions);
        });
    }

    private async Task<SKBitmap> GenerateShopImage(Shop shop, SKBitmap templateBitmap)
    {
        var imageInfo = new SKImageInfo(templateBitmap.Width,  templateBitmap.Height);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        var cornerRadius = imageInfo.Width * 0.03f;

        Console.WriteLine("Background image path: {0}", shop.BackgroundImagePath ?? "null");
        var backgroundBitmap = await _assets.GetBitmap("data/images/{0}", shop.BackgroundImagePath); // don't dispose
        Console.WriteLine("Background bitmap: {0}", backgroundBitmap is null ? "null" : "not null");
        if (backgroundBitmap is null)
        {
            using var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint((float)imageInfo.Width / 2, 0),
                new SKPoint((float)imageInfo.Width / 2, imageInfo.Height),
                new[] {new SKColor(44, 154, 234), new SKColor(14, 53, 147)},
                new[] {0.0f, 1.0f},
                SKShaderTileMode.Repeat);

            canvas.DrawRoundRect(0, 0, imageInfo.Width, imageInfo.Height, cornerRadius, cornerRadius, paint);
        }
        else
        {
            using var backgroundImagePaint = new SKPaint();
            backgroundImagePaint.IsAntialias = true;
            backgroundImagePaint.FilterQuality = SKFilterQuality.Medium;
            
            using var resizedCustomBackgroundBitmap = backgroundBitmap.Resize(imageInfo, SKFilterQuality.Medium);
            backgroundImagePaint.Shader = SKShader.CreateBitmap(resizedCustomBackgroundBitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Repeat);
            
            canvas.DrawRoundRect(0, 0, imageInfo.Width, imageInfo.Height, 50, 50, backgroundImagePaint);
        }
        canvas.DrawBitmap(templateBitmap, 0, 0);

        if (shop.CreatorCode != null)
        {
            var fortniteFont = await _assets.GetFont("Assets/Fonts/Fortnite.ttf"); // don't dispose
            using var shopTitlePaint = new SKPaint();
            shopTitlePaint.TextSize = 250.0f;
            shopTitlePaint.Typeface = fortniteFont;
            var shopTitleTextBounds = new SKRect();
            shopTitlePaint.MeasureText(shop.Title, ref shopTitleTextBounds);

            var maxBoxWidth = imageInfo.Width - 100 - shopTitleTextBounds.Width - 100 - 100;
            using var creatorCodeBoxBitmap = await GenerateCreatorCodeBox(shop.CreatorCodeTitle, shop.CreatorCode, maxBoxWidth);
            canvas.DrawBitmap(creatorCodeBoxBitmap,  imageInfo.Width - 100 - creatorCodeBoxBitmap.Width, 100);

            var adBannerBitmap = await _assets.GetBitmap("Assets/Images/Shop/ad_banner.png"); // don't dispose
            canvas.DrawBitmap(adBannerBitmap, imageInfo.Width - 100 - 50 - adBannerBitmap!.Width, 100 - adBannerBitmap.Height / 2);
        }

        return bitmap;
    }

    private async Task<SKBitmap> GenerateLocaleTemplate(Shop shop, SKBitmap templateBitmap, ShopSectionLocationData[] shopSectionLocationData)
    {
        var imageInfo = new SKImageInfo(templateBitmap.Width, templateBitmap.Height);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        canvas.DrawBitmap(templateBitmap, SKPoint.Empty);

        var fortniteFont = await _assets.GetFont(@"Assets/Fonts/Fortnite.ttf"); // don't dispose

        // Drawing the shop title
        int shopTitleWidth;
        using (var shopTitlePaint = new SKPaint())
        {
            shopTitlePaint.IsAntialias = true;
            shopTitlePaint.TextSize = 250.0f;
            shopTitlePaint.Color = SKColors.White;
            shopTitlePaint.Typeface = fortniteFont;

            var shopTitleTextBounds = new SKRect();
            shopTitlePaint.MeasureText(shop.Title, ref shopTitleTextBounds);
            shopTitleWidth = (int)shopTitleTextBounds.Width;

            canvas.DrawText(shop.Title, 100, 100 - shopTitleTextBounds.Top, shopTitlePaint);
        }

        // Drawing the date
        using (var datePaint = new SKPaint())
        {
            datePaint.IsAntialias = true;
            datePaint.TextSize = 50.0f;
            datePaint.Color = SKColors.White;
            datePaint.Typeface = fortniteFont;
            datePaint.TextAlign = SKTextAlign.Center;

            var dateTextBounds = new SKRect();
            datePaint.MeasureText(shop.Title, ref dateTextBounds);

            var datePoint = new SKPoint(
                100 + shopTitleWidth / 2f,
                300 - dateTextBounds.Top);
            canvas.DrawText(shop.Date, datePoint, datePaint);
        }

        foreach (var sectionLocationData in shopSectionLocationData)
        {
            var shopSection = shop.Sections.FirstOrDefault(x => x.Id == sectionLocationData.Id);

            // Draw the section name if it exists
            if (sectionLocationData.Name != null)
            {
                using var sectionNamePaint = new SKPaint();
                sectionNamePaint.IsAntialias = true;
                sectionNamePaint.TextSize = 45.0f;
                sectionNamePaint.Color = SKColors.White;
                sectionNamePaint.Typeface = fortniteFont;

                var sectionNameTextBounds = new SKRect();
                sectionNamePaint.MeasureText(shop.Title, ref sectionNameTextBounds);

                var sectionNamePoint = new SKPoint(sectionLocationData.Name.X,
                    sectionLocationData.Name.Y + sectionNameTextBounds.Height);
                canvas.DrawText(shopSection?.Name, sectionNamePoint, sectionNamePaint);
            }

            foreach (var entryLocationData in sectionLocationData.Entries)
            {
                var shopEntry = shopSection?.Entries?.FirstOrDefault(x => x.Id == entryLocationData.Id);
                if (shopEntry is null)
                    continue;

                // Draw the shop entry name
                using (var entryNamePaint = new SKPaint())
                {
                    entryNamePaint.IsAntialias = true;
                    entryNamePaint.TextSize = 20.0f;
                    entryNamePaint.Color = SKColors.White;
                    entryNamePaint.Typeface = fortniteFont;
                    entryNamePaint.TextAlign = SKTextAlign.Center;

                    var entryNameTextBounds = new SKRect();
                    entryNamePaint.MeasureText(shopEntry.Name, ref entryNameTextBounds);

                    var textPoint = new SKPoint(
                        entryLocationData.Name.X + entryLocationData.Name.Width!.Value / 2f,
                        entryLocationData.Name.Y + entryNameTextBounds.Height);
                    canvas.DrawText(shopEntry.Name, textPoint, entryNamePaint);
                }

                // Draw the shop entry price
                using var pricePaint = new SKPaint();
                pricePaint.IsAntialias = true;
                pricePaint.TextSize = 15.0f;
                pricePaint.Color = SKColors.White;
                pricePaint.Typeface = fortniteFont;
                pricePaint.TextAlign = SKTextAlign.Right;

                var priceTextBounds = new SKRect();

                var priceValue = shopEntry.FinalPrice.ToString();
                pricePaint.MeasureText(priceValue, ref priceTextBounds);
                var pricePoint = new SKPoint(entryLocationData.Price.X,
                    entryLocationData.Price.Y - priceTextBounds.Top);
                canvas.DrawText(priceValue, pricePoint, pricePaint);

                // Draw strikeout old price if item is discounted
                if (shopEntry.FinalPrice != shopEntry.RegularPrice)
                {
                    using var oldPricePaint = new SKPaint();
                    oldPricePaint.IsAntialias = true;
                    oldPricePaint.TextSize = 15.0f;
                    oldPricePaint.Color = new SKColor(99, 99, 99);
                    oldPricePaint.Typeface = fortniteFont;
                    oldPricePaint.TextAlign = SKTextAlign.Right;

                    var oldPriceTextBounds = new SKRect();

                    var oldPriceValue = shopEntry.RegularPrice.ToString();
                    oldPricePaint.MeasureText(oldPriceValue, ref oldPriceTextBounds);
                    var oldPricePoint = new SKPoint(entryLocationData.Price.X - oldPriceTextBounds.Width - 3,
                        entryLocationData.Price.Y - priceTextBounds.Top);
                    canvas.DrawText(oldPriceValue, oldPricePoint, oldPricePaint);

                    // Draw the strikeout line
                    using var strikePaint = new SKPaint();
                    strikePaint.IsAntialias = true;
                    strikePaint.StrokeWidth = 2.0f;
                    strikePaint.Color = new SKColor(122, 132, 133);

                    var strikeStart = new SKPoint(oldPricePoint.X - oldPriceTextBounds.Width - 3, oldPricePoint.Y - oldPriceTextBounds.Height + 7);
                    var strikeEnd = new SKPoint(oldPricePoint.X + 2,  oldPricePoint.Y - oldPriceTextBounds.Height + 3);
                    canvas.DrawLine(strikeStart, strikeEnd, strikePaint);
                }

                if (shopEntry is {BannerText: not null, BannerColor: not null})
                {
                    using var bannerBitmap = await GenerateBanner(shopEntry.BannerText, shopEntry.BannerColor);
                    canvas.DrawBitmap(bannerBitmap, entryLocationData.Banner!.X, entryLocationData.Banner.Y);
                }
            }
        }
        return bitmap;
    }

    private async Task<(ShopSectionLocationData[], SKBitmap)> GenerateTemplate(Shop shop)
    {
        var sectionWidths = new List<int[]>();
        var columns = new List<ShopSection[]>();
        SKImageInfo imageInfo;
        if (shop.Sections.Length > 6)
        {
            var breakpoint = shop.Sections.Length / 2 + shop.Sections.Length % 2;
            columns.Add(shop.Sections.Take(breakpoint).ToArray());
            sectionWidths.Add(columns[0].Select(x => (int) x.Entries.Sum(y => y.Size)).ToArray());
            columns.Add(shop.Sections.Skip(breakpoint).ToArray());
            sectionWidths.Add(columns[1].Select(x => (int) x.Entries.Sum(y => y.Size)).ToArray());
            var maxSectionWidths = sectionWidths.Select(x => x.Max()).ToArray();
            imageInfo = new SKImageInfo(
                100 + maxSectionWidths[0] * 286 + (maxSectionWidths[0] - 1) * 20 + 50
                + maxSectionWidths[1] * 286 + (maxSectionWidths[1] - 1) * 20 + 100,
                100 + 270 + (82 + 494) * columns[0].Length + 120);
        }
        else
        {
            columns.Add(shop.Sections);
            sectionWidths.Add(columns[0].Select(x => (int) x.Entries.Sum(y => y.Size)).ToArray());
            imageInfo = new SKImageInfo(100 + sectionWidths[0].Max() * 286 + (sectionWidths[0].Max() - 1) * 20 + 100,
                100 + 270 + (82 + 494) * sectionWidths[0].Length + 120);
        }

        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        var shopLocationData = new ShopSectionLocationData[shop.Sections.Length];
        for (var i = 0; i < columns.Count; i++)
        {
            var columSections = columns[i];
            for (var j = 0; j < columSections.Length; j++)
            {
                var section = columSections[j];
                var sectionImageInfo = new SKImageInfo(sectionWidths[i][j] * 286 + (sectionWidths[i][j] - 1) * 20, 494);
                using var sectionBitmap = new SKBitmap(sectionImageInfo);
                using var sectionCanvas = new SKCanvas(sectionBitmap);

                var sectionX = 100 + i * (50 + sectionWidths[0].Max() * 286 + (sectionWidths[0].Max() - 1) * 20);
                var sectionY = 100 + 270 + 100 + (82 + 494) * j;
                var shopEntryData = new List<ShopEntryLocationData>();

                var k = 0f;
                foreach (var entry in section.Entries)
                {
                    int entryX = (int) k * 286 + (int) k * 20, entryY = MathF.Floor(k).Equals(k) ? 0 : 237 + 20;
                    using var itemCardBitmap = await GenerateItemCard(entry);
                    sectionCanvas.DrawBitmap(itemCardBitmap, new SKPoint(entryX, entryY));

                    k += entry.Size;

                    var nameLocationData = new ShopLocationDataEntry(sectionX + entryX,
                        sectionY + entryY + itemCardBitmap.Height - 52, itemCardBitmap.Width);
                    var priceLocationData = new ShopLocationDataEntry(sectionX + entryX + itemCardBitmap.Width - 41,
                        sectionY + entryY + itemCardBitmap.Height - 19);
                    ShopLocationDataEntry? bannerLocationData = null;
                    if (entry.BannerText != null)
                        bannerLocationData = new ShopLocationDataEntry(sectionX + entryX - 7, sectionY + entryY - 7);
                    shopEntryData.Add(new ShopEntryLocationData(entry.Id, nameLocationData, priceLocationData,
                        bannerLocationData));
                }

                ShopLocationDataEntry? sectionNameLocationData = null;
                if (section.Name != null)
                    sectionNameLocationData = new ShopLocationDataEntry(sectionX + 28, sectionY - 45 - 8);

                shopLocationData[j + i * columns[0].Length] = new ShopSectionLocationData(section.Id, sectionNameLocationData, shopEntryData.ToArray());

                canvas.DrawBitmap(sectionBitmap, new SKPoint(sectionX, sectionY));
            }
        }

        using (var footerPaint = new SKPaint())
        {
            const string footerText = "SHOP-DATA PROVIDED BY FORTNITE-API.COM";

            var fortniteFont = await _assets.GetFont(@"Assets/Fonts/Fortnite.ttf"); // don't dispose
            footerPaint.IsAntialias = true;
            footerPaint.Color = SKColors.White;
            footerPaint.TextSize = 50.0f;
            footerPaint.Typeface = fortniteFont;
            footerPaint.TextAlign = SKTextAlign.Center;

            var footerBounds = new SKRect();
            footerPaint.MeasureText(footerText, ref footerBounds);

            canvas.DrawText(footerText, (float)imageInfo.Width / 2, imageInfo.Height + footerBounds.Top, footerPaint);
        }

        return (shopLocationData, bitmap);
    }

    private async Task<SKBitmap> GenerateCreatorCodeBox(string creatorCodeTitle, string creatorCode, float maxWidth)
    {
        var fortniteFont = await _assets.GetFont(@"Assets/Fonts/Fortnite.ttf"); // don't dispose

        using var creatorCodeTitlePaint = new SKPaint();
        creatorCodeTitlePaint.IsAntialias = true;
        creatorCodeTitlePaint.TextSize = 130.0f;
        creatorCodeTitlePaint.Typeface = fortniteFont;
        creatorCodeTitlePaint.Color = SKColors.White;

        var creatorCodeTitleBounds = new SKRect();
        creatorCodeTitlePaint.MeasureText(creatorCodeTitle, ref creatorCodeTitleBounds);

        using var creatorCodePaint = new SKPaint();
        creatorCodePaint.IsAntialias = true;
        creatorCodePaint.TextSize = 130.0f;
        creatorCodePaint.Typeface = fortniteFont;
        creatorCodePaint.Color = SKColors.White;
        creatorCodePaint.TextAlign = SKTextAlign.Right;

        var creatorCodeBounds = new SKRect();
        creatorCodeTitlePaint.MeasureText(creatorCode, ref creatorCodeBounds);

        int height = 200, splitHeight = 150;
        while (50 + creatorCodeTitleBounds.Width + 30 + 15 + 30 + creatorCodeBounds.Width + 50 > maxWidth)
        {
            creatorCodeTitlePaint.TextSize--;
            creatorCodeTitlePaint.MeasureText(creatorCodeTitle, ref creatorCodeTitleBounds);
            creatorCodePaint.TextSize--;
            creatorCodePaint.MeasureText(creatorCode, ref creatorCodeBounds);
            height--;
            splitHeight--;
        }

        var imageInfo = new SKImageInfo(
            50 + (int)creatorCodeTitleBounds.Width + 30 + 15 + 30 + (int)creatorCodeBounds.Width + 50, height);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        using (var boxPaint = new SKPaint())
        {
            boxPaint.IsAntialias = true;
            boxPaint.Color = SKColors.White.WithAlpha((int) (.5 * 255));
            boxPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(new SKRect(0, 0, imageInfo.Width, imageInfo.Height), 100, 100, boxPaint);
        }

        canvas.DrawText(creatorCodeTitle, 50, (float)imageInfo.Height / 2 - creatorCodeTitleBounds.MidY, creatorCodeTitlePaint);
        canvas.DrawText(creatorCode, imageInfo.Width - 50, (float)imageInfo.Height / 2 - creatorCodeBounds.MidY, creatorCodePaint);

        using (var splitPaint = new SKPaint())
        {
            splitPaint.IsAntialias = true;
            splitPaint.Color = SKColors.White.WithAlpha((int) (.3 * 255));
            splitPaint.Style = SKPaintStyle.Fill;

            canvas.DrawRoundRect(50 + creatorCodeTitleBounds.Width + 30,
                (float)(imageInfo.Height - splitHeight) / 2, 15, splitHeight, 10, 10, splitPaint);
        }

        return bitmap;
    }

    private async Task<SKBitmap> GenerateBanner(string text, string[] colors)
    {
        var fortniteFont = await _assets.GetFont(@"Assets/Fonts/Fortnite.ttf"); // don't dispose
        using var bannerPaint = new SKPaint();
        bannerPaint.IsAntialias = true;
        bannerPaint.TextSize = 15.0f;
        bannerPaint.Typeface = fortniteFont;
        bannerPaint.Color = SKColor.Parse(colors[2]);

        var textBounds = new SKRect();
        bannerPaint.MeasureText(text, ref textBounds);

        var imageInfo = new SKImageInfo(9 + (int)textBounds.Width + 8, 31);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        using (var outerBorderPaint = new SKPaint())
        {
            outerBorderPaint.IsAntialias = true;
            outerBorderPaint.Color = SKColor.Parse(colors[0]);
            outerBorderPaint.Style = SKPaintStyle.Fill;

            using var path = new SKPath();
            path.MoveTo(0, 2);
            path.LineTo(imageInfo.Width, 0);
            path.LineTo(imageInfo.Width - 6, imageInfo.Height);
            path.LineTo(3, imageInfo.Height - 1);
            path.Close();

            canvas.DrawPath(path, outerBorderPaint);
        }

        using (var innerBorderPaint = new SKPaint())
        {
            innerBorderPaint.IsAntialias = true;
            innerBorderPaint.Color = SKColor.Parse(colors[1]);
            innerBorderPaint.Style = SKPaintStyle.Fill;

            using var path = new SKPath();
            path.MoveTo(4, 6);
            path.LineTo(imageInfo.Width - 5, 4);
            path.LineTo(imageInfo.Width - 9, imageInfo.Height - 3);
            path.LineTo(6, imageInfo.Height - 4);
            path.Close();

            canvas.DrawPath(path, innerBorderPaint);
        }
        // 6 + textBounds.Top
        canvas.DrawText(text, 9, (float)imageInfo.Height / 2 - textBounds.MidY, bannerPaint);

        return bitmap;
    }

    private async Task<SKBitmap> GenerateItemCard(ShopEntry shopEntry)
    {
        var imageInfo = new SKImageInfo(
            (int) Math.Ceiling(shopEntry.Size) * 286 + ((int) Math.Ceiling(shopEntry.Size) - 1) * 20,
            Math.Floor(shopEntry.Size).Equals(shopEntry.Size) ? 494 : 237);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        var imageResize = shopEntry.Size.Equals(1.0f) ? 429 : imageInfo.Width;
        using var resizedImageBitmap =
            shopEntry.Image!.Resize(new SKImageInfo(imageResize, imageResize), SKFilterQuality.Medium);

        if (shopEntry.ImageUrl == null)
        {
            // Draw radial gradient and paste resizedImageBitmap on it
            using var gradientPaint = new SKPaint();
            gradientPaint.IsAntialias = true;
            gradientPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(imageInfo.Rect.MidX, imageInfo.Rect.MidY),
                MathF.Sqrt(MathF.Pow(imageInfo.Rect.MidX, 2) + MathF.Pow(imageInfo.Rect.MidY, 2)),
                new SKColor[] { new(129, 207, 250), new(52, 136, 217)},
                SKShaderTileMode.Clamp);

            canvas.DrawRect(0, 0, imageInfo.Width, imageInfo.Height, gradientPaint);
        }

        if (shopEntry.Size.Equals(1.0f))
        {
            // Crop resized bitmap to copXY
            var cropX = (resizedImageBitmap.Width - imageInfo.Width) / 2;
            var cropRect = new SKRect(cropX, 0, cropX + imageInfo.Width, resizedImageBitmap.Height);
            canvas.DrawBitmap(resizedImageBitmap, cropRect,
                new SKRect(0, 0, imageInfo.Width, resizedImageBitmap.Height));
        }
        else canvas.DrawBitmap(resizedImageBitmap, SKPoint.Empty);

        var vbucksBitmap = await _assets.GetBitmap(@"Assets/Images/Shop/vbucks_icon.png"); // don't dispose
        using var overlayImage = ImageUtils.GenerateItemCardOverlay(imageInfo.Width, vbucksBitmap);
        canvas.DrawBitmap(overlayImage, new SKPoint(0, imageInfo.Height - overlayImage.Height));

        using var rarityStripe = ImageUtils.GenerateRarityStripe(imageInfo.Width, SKColor.Parse(shopEntry.RarityColor));
        canvas.DrawBitmap(rarityStripe,
            new SKPoint(0, imageInfo.Height - overlayImage.Height - rarityStripe.Height + 5));
        // TODO: Fix Transparency issues

        if (!shopEntry.Special) return bitmap;
        using var paint = new SKPaint();
        paint.IsAntialias = true;
        paint.TextSize = 60.0f;
        paint.Color = SKColors.White;
        var fortniteFont = await _assets.GetFont(@"Assets/Fonts/Fortnite.ttf"); // don't dispose
        paint.Typeface = fortniteFont;
        paint.TextAlign = SKTextAlign.Right;

        var textBounds = new SKRect();
        paint.MeasureText("+", ref textBounds);

        canvas.DrawText("+", imageInfo.Width - 10, imageInfo.Height - overlayImage.Height - 15, paint);

        return bitmap;
    }
}
