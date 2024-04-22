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

    // Constants
    private const int HORIZONTAL_PADDING = 100;
    private const int BOTTOM_PADDING = 100;
    private const int HEADER_HEIGHT = 450;
    private const int COLUMN_SPACE = 100;
    private const int CARD_WIDTH = 220;
    private const int CARD_HEIGHT = 552;
    private const int CARD_SPACE = 24;
    private const int CARDS_PER_SECTION = 5;
    private const int SECTION_WIDTH = CARDS_PER_SECTION * CARD_WIDTH + (CARDS_PER_SECTION - 1) * CARD_SPACE;
    private const int SECTION_HEIGHT = CARD_HEIGHT + 57;

    private const float TITLE_FONT_SIZE = 200f;
    private const float DATE_FONT_SIZE = 50f;
    private const float SECTION_NAME_FONT_SIZE = 43f;
    private const float ENTRY_NAME_FONT_SIZE = 27f;
    private const float ENTRY_PRICE_FONT_SIZE = 21f;


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

    public ShopImageController(IMemoryCache cache, IHttpClientFactory clientFactory, NamedLock namedLock,
        SharedAssets assets)
    {
        _cache = cache;
        _clientFactory = clientFactory;
        _namedLock = namedLock;
        _assets = assets;
    }

    [HttpPost]
    public async Task<IActionResult> Post(Shop shop)
    {
        Console.WriteLine($"Item Shop image request | Locale = {shop.Locale} | New Shop = {shop.NewShop}");
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
            await _namedLock.WaitAsync(cacheKey, token);
            var cachedBitmap = _cache.Get<SKBitmap?>(cacheKey);
            if (cachedBitmap is not null)
            {
                entry.Image = cachedBitmap;
                _namedLock.Release(cacheKey);
                return;
            }

            using var client = _clientFactory.CreateClient();
            var url = entry.ImageUrl ?? entry.FallbackImageUrl;
            SKBitmap bitmap;

            try
            {
                var imageBytes = await client.GetByteArrayAsync(url, token);
                bitmap = SKBitmap.Decode(imageBytes);
            }
            catch (Exception)
            {
                bitmap = new SKBitmap(512, 512);
            }

            entry.Image = bitmap;
            // cache image for 10 minutes & make sure it gets disposed after the period
            _cache.Set(cacheKey, bitmap, ShopImageCacheOptions);
            _namedLock.Release(cacheKey);
        });
    }

    private async Task<SKBitmap> GenerateShopImage(Shop shop, SKBitmap templateBitmap)
    {
        var imageInfo = new SKImageInfo(templateBitmap.Width, templateBitmap.Height);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        var backgroundBitmap = await _assets.GetBitmap("data/images/{0}", shop.BackgroundImagePath); // don't dispose
        if (backgroundBitmap is null)
        {
            using var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint((float)imageInfo.Width / 2, 0),
                new SKPoint((float)imageInfo.Width / 2, imageInfo.Height),
                [new SKColor(44, 154, 234), new SKColor(14, 53, 147)],
                [0.0f, 1.0f],
                SKShaderTileMode.Repeat);

            canvas.DrawRoundRect(0, 0, imageInfo.Width, imageInfo.Height, 50, 50, paint);
        }
        else
        {
            using var backgroundImagePaint = new SKPaint();
            backgroundImagePaint.IsAntialias = true;
            backgroundImagePaint.FilterQuality = SKFilterQuality.Medium;

            using var resizedCustomBackgroundBitmap = backgroundBitmap.Resize(imageInfo, SKFilterQuality.Medium);
            backgroundImagePaint.Shader = SKShader.CreateBitmap(resizedCustomBackgroundBitmap, SKShaderTileMode.Clamp,
                SKShaderTileMode.Repeat);

            canvas.DrawRoundRect(0, 0, imageInfo.Width, imageInfo.Height, 50, 50, backgroundImagePaint);
        }

        canvas.DrawBitmap(templateBitmap, 0, 0);

        if (shop.CreatorCode != null)
        {
            var fortniteFont = await _assets.GetFont("Assets/Fonts/Fortnite.ttf"); // don't dispose
            using var shopTitlePaint = new SKPaint();
            shopTitlePaint.TextSize = TITLE_FONT_SIZE;
            shopTitlePaint.Typeface = fortniteFont;
            var shopTitleTextBounds = new SKRect();
            shopTitlePaint.MeasureText(shop.Title, ref shopTitleTextBounds);

            var maxBoxWidth = imageInfo.Width - 100 - shopTitleTextBounds.Width - 100 - 100;
            using var creatorCodeBoxBitmap =
                await GenerateCreatorCodeBox(shop.CreatorCodeTitle, shop.CreatorCode, maxBoxWidth);
            canvas.DrawBitmap(creatorCodeBoxBitmap, imageInfo.Width - 100 - creatorCodeBoxBitmap.Width, 100);

            var adBannerBitmap = await _assets.GetBitmap("Assets/Images/Shop/ad_banner.png"); // don't dispose
            canvas.DrawBitmap(adBannerBitmap, imageInfo.Width - 100 - 50 - adBannerBitmap!.Width,
                100 - adBannerBitmap.Height / 2);
        }

        return bitmap;
    }

    private async Task<SKBitmap> GenerateLocaleTemplate(Shop shop, SKBitmap templateBitmap,
        IEnumerable<ShopSectionLocationData> shopSectionLocationData)
    {
        var imageInfo = new SKImageInfo(templateBitmap.Width, templateBitmap.Height);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        canvas.DrawBitmap(templateBitmap, SKPoint.Empty);

        // Drawing the shop title
        int shopTitleWidth;
        using (var shopTitlePaint = new SKPaint())
        {
            shopTitlePaint.IsAntialias = true;
            shopTitlePaint.TextSize = TITLE_FONT_SIZE;
            shopTitlePaint.Color = SKColors.White;
            shopTitlePaint.Typeface = await _assets.GetFont(@"Assets/Fonts/HeadingNow_86Bold.otf");

            var shopTitleTextBounds = new SKRect();
            shopTitlePaint.MeasureText(shop.Title, ref shopTitleTextBounds);
            shopTitleWidth = (int)shopTitleTextBounds.Width;

            canvas.DrawText(shop.Title, 100, 50 - shopTitleTextBounds.Top, shopTitlePaint);
        }

        // Drawing the date
        using (var datePaint = new SKPaint())
        {
            datePaint.IsAntialias = true;
            datePaint.TextSize = DATE_FONT_SIZE;
            datePaint.Color = SKColors.White;
            datePaint.Typeface = await _assets.GetFont(@"Assets/Fonts/HeadingNow_86BoldItalic.otf");
            datePaint.TextAlign = SKTextAlign.Center;

            var dateTextBounds = new SKRect();
            datePaint.MeasureText(shop.Date, ref dateTextBounds);

            var datePoint = new SKPoint(
                Math.Max(HORIZONTAL_PADDING + shopTitleWidth / 2f, HORIZONTAL_PADDING + dateTextBounds.Width / 2),
                313 - dateTextBounds.Top);
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
                sectionNamePaint.TextSize = SECTION_NAME_FONT_SIZE;
                sectionNamePaint.Color = SKColors.White;
                sectionNamePaint.Typeface = await _assets.GetFont(@"Assets/Fonts/HeadingNow_86BoldItalic.otf");

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
                    entryNamePaint.TextSize = ENTRY_NAME_FONT_SIZE;
                    entryNamePaint.Color = SKColors.White;
                    entryNamePaint.Typeface = await _assets.GetFont(@"Assets/Fonts/HeadingNow_75Medium.otf");

                    var entryNameTextBounds = new SKRect();
                    entryNamePaint.MeasureText(shopEntry.Name, ref entryNameTextBounds);

                    var textPoint = new SKPoint(entryLocationData.Name.X,
                        entryLocationData.Name.Y + entryNameTextBounds.Height);
                    canvas.DrawText(shopEntry.Name, textPoint, entryNamePaint);
                }

                // Draw the shop entry price
                using var pricePaint = new SKPaint();
                pricePaint.IsAntialias = true;
                pricePaint.TextSize = ENTRY_PRICE_FONT_SIZE;
                pricePaint.Color = SKColors.White;
                pricePaint.Typeface = await _assets.GetFont(@"Assets/Fonts/HeadingNow_75Medium.otf");

                var priceTextBounds = new SKRect();

                var priceValue = shopEntry.FinalPrice;
                pricePaint.MeasureText(priceValue, ref priceTextBounds);
                var pricePoint = new SKPoint(entryLocationData.Price.X,
                    entryLocationData.Price.Y - priceTextBounds.Top);
                canvas.DrawText(priceValue, pricePoint, pricePaint);

                // Draw strikeout old price if item is discounted
                if (shopEntry.FinalPrice != shopEntry.RegularPrice)
                {
                    using var oldPricePaint = new SKPaint();
                    oldPricePaint.IsAntialias = true;
                    oldPricePaint.TextSize = ENTRY_PRICE_FONT_SIZE;
                    oldPricePaint.Color = SKColors.White.WithAlpha((int)(.6 * 255));
                    oldPricePaint.Typeface = await _assets.GetFont(@"Assets/Fonts/HeadingNow_75Medium.otf");

                    var oldPriceTextBounds = new SKRect();

                    var oldPriceValue = shopEntry.RegularPrice;
                    oldPricePaint.MeasureText(oldPriceValue, ref oldPriceTextBounds);
                    var oldPricePoint = new SKPoint(entryLocationData.Price.X + oldPriceTextBounds.Width + 9,
                        entryLocationData.Price.Y - priceTextBounds.Top);
                    canvas.DrawText(oldPriceValue, oldPricePoint, oldPricePaint);

                    // Draw the strikeout line
                    using var strikePaint = new SKPaint();
                    strikePaint.IsAntialias = true;
                    strikePaint.StrokeWidth = 2f;
                    strikePaint.Color = SKColors.White.WithAlpha((int)(.6 * 255));

                    var strikeStart = new SKPoint(oldPricePoint.X - 4, oldPricePoint.Y - 15);
                    var strikeEnd = new SKPoint(oldPricePoint.X + oldPriceTextBounds.Width + 2, oldPricePoint.Y - 12);
                    canvas.DrawLine(strikeStart, strikeEnd, strikePaint);
                }

                if (shopEntry.Banner != null)
                {
                    using var bannerBitmap = await GenerateBanner(shopEntry.Banner.Text, shopEntry.Banner.Colors);
                    canvas.DrawBitmap(bannerBitmap, entryLocationData.Banner!.X, entryLocationData.Banner.Y);
                }
            }
        }

        return bitmap;
    }

    private async Task<(ShopSectionLocationData[], SKBitmap)> GenerateTemplate(Shop shop)
    {
        var columnCount = 2;
        var bestAspectRatioDiff = float.MaxValue;
        int width = 0, height = 0, sectionsPerColumn = 0;
        for (var i = columnCount; i <= 15; i++)
        {
            // Calculate total width of the image
            width = HORIZONTAL_PADDING * 2 + i * SECTION_WIDTH + (i - 1) * COLUMN_SPACE;
            // Calculate the number of sections per column
            sectionsPerColumn = (int)Math.Ceiling((double)shop.Sections.Length / columnCount);
            // Calculate height of the image
            height = HEADER_HEIGHT + sectionsPerColumn * SECTION_HEIGHT + (sectionsPerColumn - 1) * CARD_SPACE +
                     BOTTOM_PADDING;
            // Calculate aspect ratio
            var aspectRatio = (float)width / height;
            // Calculate the difference between current aspect ratio and 1:1
            var aspectRatioDiff = Math.Abs(aspectRatio - 1);
            // Check if this aspect ratio is closer to 1:1
            if (!(aspectRatioDiff < bestAspectRatioDiff)) break;
            bestAspectRatioDiff = aspectRatioDiff;
            columnCount = i;
        }

        var imageInfo = new SKImageInfo(width, height);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        var shopLocationData = new ShopSectionLocationData[shop.Sections.Length];
        var iSec = 0;
        for (var i = 0; i < columnCount; i++)
        {
            var sections = shop.Sections.Skip(i * sectionsPerColumn).Take(sectionsPerColumn).ToList();
            for (var j = 0; j < sections.Count; j++)
            {
                var section = sections[j];
                var sectionImageInfo = new SKImageInfo(SECTION_WIDTH, SECTION_HEIGHT);
                using var sectionBitmap = new SKBitmap(sectionImageInfo);
                using var sectionCanvas = new SKCanvas(sectionBitmap);

                var sectionX = HORIZONTAL_PADDING + i * SECTION_WIDTH + i * COLUMN_SPACE;
                var sectionY = HEADER_HEIGHT + j * SECTION_HEIGHT + j * CARD_SPACE;

                var position = 0f;
                var shopEntryData = new List<ShopEntryLocationData>();
                foreach (var entry in section.Entries)
                {
                    // If the next card is full height, we can't fit it in the current column
                    if (!MathF.Floor(position).Equals(position) && entry.Size >= 1) position = MathF.Ceiling(position);
                    var entryX = (int)position * CARD_WIDTH + (int)position * CARD_SPACE;
                    var entryY = SECTION_HEIGHT - CARD_HEIGHT +
                                 (MathF.Floor(position).Equals(position) ? 0 : (CARD_HEIGHT + CARD_SPACE) / 2);
                    position += entry.Size;

                    using var itemCardBitmap = await GenerateItemCard(entry);
                    using var itemCardPaint = new SKPaint();
                    itemCardPaint.IsAntialias = true;
                    itemCardPaint.Shader = SKShader.CreateBitmap(itemCardBitmap, SKShaderTileMode.Clamp,
                        SKShaderTileMode.Clamp, SKMatrix.CreateTranslation(entryX, entryY));
                    sectionCanvas.DrawRoundRect(entryX, entryY, itemCardBitmap.Width, itemCardBitmap.Height, 20, 20,
                        itemCardPaint);

                    var nameLocationData = new ShopLocationDataEntry(sectionX + entryX + 13,
                        sectionY + entryY + itemCardBitmap.Height - 72, itemCardBitmap.Width);
                    var priceLocationData = new ShopLocationDataEntry(sectionX + entryX + 13 + 22 + 8,
                        sectionY + entryY + itemCardBitmap.Height - 36);
                    ShopLocationDataEntry? bannerLocationData = null;
                    if (entry.Banner != null)
                        bannerLocationData = new ShopLocationDataEntry(sectionX + entryX + 8, sectionY + entryY + 8,
                            itemCardBitmap.Width - 2 * 8);
                    shopEntryData.Add(new ShopEntryLocationData(entry.Id, nameLocationData, priceLocationData,
                        bannerLocationData));
                }


                ShopLocationDataEntry? sectionNameLocationData = null;
                if (section.Name != null)
                    sectionNameLocationData = new ShopLocationDataEntry(sectionX, sectionY);
                shopLocationData[iSec] =
                    new ShopSectionLocationData(section.Id, sectionNameLocationData, shopEntryData.ToArray());

                canvas.DrawBitmap(sectionBitmap, new SKPoint(sectionX, sectionY));
                iSec++;
            }
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
            boxPaint.Color = SKColors.White.WithAlpha((int)(.5 * 255));
            boxPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(new SKRect(0, 0, imageInfo.Width, imageInfo.Height), 100, 100, boxPaint);
        }

        canvas.DrawText(creatorCodeTitle, 50, (float)imageInfo.Height / 2 - creatorCodeTitleBounds.MidY,
            creatorCodeTitlePaint);
        canvas.DrawText(creatorCode, imageInfo.Width - 50, (float)imageInfo.Height / 2 - creatorCodeBounds.MidY,
            creatorCodePaint);

        using (var splitPaint = new SKPaint())
        {
            splitPaint.IsAntialias = true;
            splitPaint.Color = SKColors.White.WithAlpha((int)(.3 * 255));
            splitPaint.Style = SKPaintStyle.Fill;

            canvas.DrawRoundRect(50 + creatorCodeTitleBounds.Width + 30,
                (float)(imageInfo.Height - splitHeight) / 2, 15, splitHeight, 10, 10, splitPaint);
        }

        return bitmap;
    }

    private async Task<SKBitmap> GenerateBanner(string text, IReadOnlyList<string> colors)
    {
        using var bannerPaint = new SKPaint();
        bannerPaint.IsAntialias = true;
        bannerPaint.TextSize = 17.0f;
        bannerPaint.Typeface = await _assets.GetFont(@"Assets/Fonts/HeadingNow_76BoldItalic.otf");
        bannerPaint.Color = SKColor.Parse(colors[1]);

        var textBounds = new SKRect();
        bannerPaint.MeasureText(text, ref textBounds);

        var imageInfo = new SKImageInfo(13 + (int)textBounds.Width + 13, 34);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        using (var backgroundPaint = new SKPaint())
        {
            backgroundPaint.IsAntialias = true;
            backgroundPaint.Color = SKColor.Parse(colors[0]);
            backgroundPaint.Style = SKPaintStyle.Fill;

            canvas.DrawRoundRect(new SKRect(0, 0, imageInfo.Width, imageInfo.Height), 20, 20, backgroundPaint);
        }

        // 6 + textBounds.Top
        canvas.DrawText(text, 13, (float)imageInfo.Height / 2 - textBounds.MidY, bannerPaint);

        return bitmap;
    }

    private async Task<SKBitmap> GenerateItemCard(ShopEntry shopEntry)
    {
        var imageInfo = new SKImageInfo(
            (int)Math.Ceiling(shopEntry.Size) * CARD_WIDTH + ((int)Math.Ceiling(shopEntry.Size) - 1) * CARD_SPACE,
            Math.Floor(shopEntry.Size).Equals(shopEntry.Size) ? CARD_HEIGHT : CARD_HEIGHT / 2 - CARD_SPACE / 2);
        var bitmap = new SKBitmap(imageInfo);

        if (shopEntry.Image is null)
            return bitmap;

        using var canvas = new SKCanvas(bitmap);

        // Scale image down to fit the card
        var imageResize = Math.Max(imageInfo.Width, imageInfo.Height);
        using var resizedImageBitmap =
            shopEntry.Image.Resize(new SKImageInfo(imageResize, imageResize), SKFilterQuality.Medium);

        // Generate background gradient for items that come without
        if (shopEntry.ImageUrl == null)
        {
            // Draw radial gradient and paste resizedImageBitmap on it
            using var gradientPaint = new SKPaint();
            gradientPaint.IsAntialias = true;
            gradientPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(imageInfo.Rect.MidX, imageInfo.Rect.MidY),
                MathF.Sqrt(MathF.Pow(imageInfo.Rect.MidX, 2) + MathF.Pow(imageInfo.Rect.MidY, 2)),
                [new SKColor(129, 207, 250), new SKColor(52, 136, 217)],
                SKShaderTileMode.Clamp);

            canvas.DrawRect(0, 0, imageInfo.Width, imageInfo.Height, gradientPaint);
        }

        // Center image in the middle of the card, if width is bigger than the image
        if (resizedImageBitmap.Width > imageInfo.Width)
        {
            var cropX = (resizedImageBitmap.Width - imageInfo.Width) / 2;
            var cropRect = new SKRect(cropX, 0, cropX + imageInfo.Width, resizedImageBitmap.Height);
            canvas.DrawBitmap(resizedImageBitmap, cropRect,
                new SKRect(0, 0, imageInfo.Width, resizedImageBitmap.Height));
        }
        else canvas.DrawBitmap(resizedImageBitmap, SKPoint.Empty);

        // Draw V-Bucks icon
        var vbucksBitmap = await _assets.GetBitmap(@"Assets/Images/Shop/vbucks_icon.png"); // don't dispose
        canvas.DrawBitmap(vbucksBitmap, 13, imageInfo.Height - vbucksBitmap!.Height - 11);

        if (shopEntry.IsSpecial)
        {
            using var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.TextSize = 35.0f;
            paint.Color = SKColors.White;
            paint.Typeface = await _assets.GetFont(@"Assets/Fonts/HeadingNow_74Regular.otf");
            paint.TextAlign = SKTextAlign.Right;

            var textBounds = new SKRect();
            paint.MeasureText("+", ref textBounds);

            canvas.DrawText("+", imageInfo.Width - 18, imageInfo.Height - textBounds.Height + 3,
                paint);
        }

        return bitmap;
    }
}