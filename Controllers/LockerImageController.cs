using System.Net;

using EasyFortniteStats_ImageApi.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

using SkiaSharp;

// ReSharper disable InconsistentNaming
namespace EasyFortniteStats_ImageApi.Controllers;

[ApiController]
[Route("locker")]
public class AccountImageController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _clientFactory;
    private readonly NamedLock _namedLock;
    private readonly SharedAssets _assets;
    private static readonly IReadOnlyList<(int Count, int Quality)> QualityMapping = new List<(int, int)>
    {
        (500, 85),
        (400, 90),
        (300, 95),
        //(200, 100),
        (0, 100),
    };

    public AccountImageController(IMemoryCache cache, IHttpClientFactory clientFactory, NamedLock namedLock, SharedAssets assets)
    {
        _cache = cache;
        _clientFactory = clientFactory;
        _namedLock = namedLock;
        _assets = assets;
    }

    private static readonly MemoryCacheEntryOptions LockerImageCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
        PostEvictionCallbacks =
        {
            new PostEvictionCallbackRegistration
            {
                EvictionCallback = ImageUtils.BitmapPostEvictionCallback
            }
        }
    };

    [HttpPost]
    public async Task<IActionResult> Post(Locker locker)
    {
        Console.WriteLine($"Locker image request. Name = {locker.PlayerName}, Locale = {locker.Locale}, Items = {locker.Items.Length}");
        var lockKey = $"locker_{locker.RequestId}";
        await _namedLock.WaitAsync(lockKey);
        try
        {
            await GenerateItemCards(locker);
        }
        finally
        {
            _namedLock.Release(lockKey);
        }
        using var lockerBitmap = await GenerateImage(locker);

        // Determine the quality of the image based on quality mapping and locker.Items.Length
        var quality = QualityMapping.FirstOrDefault(x => locker.Items.Length >= x.Count).Quality;
        var data = lockerBitmap.Encode(SKEncodedImageFormat.Jpeg, quality);
        return File(data.AsStream(true), "image/jpeg");
    }

    private async Task<SKBitmap> GenerateImage(Locker locker)
    {
        // Calculate rows and columns based on locker.Items count
        // Columns and Rows should be equal
        // min value is 5
        var columns = Math.Max((int) Math.Ceiling(Math.Sqrt(locker.Items.Length)), 5);
        var rows = locker.Items.Length / columns + (locker.Items.Length % columns == 0 ? 0 : 1);

        var uiResizingFactor = (float)(1 + rows * 0.15);

        var nameFontSize = (int)(64 * uiResizingFactor);

        var footerSpace = (int) (80 * uiResizingFactor);
        var imageInfo = new SKImageInfo(
            50 + 256 * columns + 25 * (columns - 1) + 50, 
            50 + nameFontSize + 50 + rows * 313 + (rows - 1) * 25 + footerSpace);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        
        using var backgroundPaint = new SKPaint();
        backgroundPaint.IsAntialias = true;
        backgroundPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint((float)imageInfo.Width / 2, 0),
            new SKPoint((float)imageInfo.Width / 2, imageInfo.Height),
            new[] {new SKColor(44, 154, 234), new SKColor(14, 53, 147)},
            new[] {0.0f, 1.0f},
            SKShaderTileMode.Repeat);
        
        canvas.DrawRect(0, 0, imageInfo.Width, imageInfo.Height, backgroundPaint);
        
        var textBounds = new SKRect();
        var segoeFont = await _assets.GetFont("Assets/Fonts/Segoe.ttf"); // don't dispose
        
        var icon = await _assets.GetBitmap("Assets/Images/Locker/Icon.png");  // don't dispose
        var resize = (int) (50 * uiResizingFactor);
        using var resizeIcon = icon!.Resize(new SKImageInfo(resize, resize), SKFilterQuality.High);
        canvas.DrawBitmap(resizeIcon, 50, 50);

        using var splitPaint = new SKPaint();
        splitPaint.IsAntialias = true;
        splitPaint.Color = SKColors.White;

        var splitWidth = 5 * uiResizingFactor;
        var splitR = 3 * uiResizingFactor;
        canvas.DrawRoundRect(50 + resizeIcon.Width + splitWidth, 57, splitWidth, 50 * uiResizingFactor, splitR, splitR, splitPaint);
        
        using var namePaint = new SKPaint();
        namePaint.IsAntialias = true;
        namePaint.Color = SKColors.White;
        namePaint.Typeface = segoeFont;
        namePaint.TextSize = nameFontSize;
        namePaint.FilterQuality = SKFilterQuality.Medium;
        
        namePaint.MeasureText(locker.PlayerName, ref textBounds);
        canvas.DrawText(locker.PlayerName, 50 + resizeIcon.Width + splitWidth * 3, 58 - textBounds.Top, namePaint);

        using var discordBoxBitmap = await ImageUtils.GenerateDiscordBox(_assets, locker.UserName, uiResizingFactor);
        canvas.DrawBitmap(discordBoxBitmap, imageInfo.Width - 50 - discordBoxBitmap.Width, 39);

        var column = 0; var row = 0;
        foreach (var item in locker.Items)
        {
            canvas.DrawBitmap(
                item.Image, 
                50 + 256 * column + 25 * column, 
                50 + nameFontSize + 50 + row * 313 + row * 25);
            column++;
            if (column != columns) continue;
            column = 0;
            row++;
        }
        
        // Load Footer.svg file as a stream
        using var footerBitmap = await GenerateFooter(uiResizingFactor);
        canvas.DrawBitmap(footerBitmap,  (imageInfo.Width - footerBitmap.Width) / 2.0f, imageInfo.Height - (footerSpace + footerBitmap.Height) / 2.0f);

        return bitmap;
    }

    private async Task GenerateItemCards(Locker locker)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount / 2
        };
        await Parallel.ForEachAsync(locker.Items, options, async (item, token) =>
        {
            var itemCardKey = $"locker_card_image_{item.Id}_{locker.Locale}";
            var itemCard = _cache.Get<SKBitmap?>(itemCardKey);
            if (itemCard is null)
            {
                var itemImageKey = $"locker_image_{item.Id}";
                var itemImage = _cache.Get<SKBitmap?>(itemImageKey);
                if (itemImage is null)
                {
                    using var client = _clientFactory.CreateClient();
                    byte[] itemImageBytes;
                    try
                    {
                        itemImageBytes = await client.GetByteArrayAsync(item.ImageUrl, token);
                    }
                    catch (HttpRequestException e)
                    {
                        if (e.StatusCode != HttpStatusCode.NotFound) throw;
                        itemImageBytes = await client.GetByteArrayAsync(item.ImageUrl.Replace("_256", ""), token);
                    }

                    var itemImageRaw = SKBitmap.Decode(itemImageBytes);
                    if (itemImageRaw.Width != 256 || itemImageRaw.Height != 256)
                    {
                        itemImage = itemImageRaw.Resize(new SKImageInfo(256, 256), SKFilterQuality.Medium);
                        itemImageRaw.Dispose();
                    }
                    else
                    {
                        itemImage = itemImageRaw;
                    }

                    _cache.Set(itemImageKey, itemImage, LockerImageCacheOptions);
                }

                itemCard = await GenerateItemCard(item, itemImage);
                _cache.Set(itemCardKey, itemCard, LockerImageCacheOptions);
            }

            item.Image = itemCard;
        });
    }

    private async Task<SKBitmap> GenerateItemCard(LockerItem lockerItem, SKBitmap itemImage)
    {
        var imageInfo = new SKImageInfo(256, 313);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        var rarityBackground = await _assets.GetBitmap($"Assets/Images/Locker/RarityBackgrounds/{lockerItem.Rarity}.png");
        canvas.DrawBitmap(rarityBackground, SKPoint.Empty);

        canvas.DrawBitmap(itemImage, SKPoint.Empty);

        var typeIcon = lockerItem.SourceType != SourceType.Other ? await _assets.GetBitmap($"Assets/Images/Locker/Source/{lockerItem.SourceType}.png") : null;
        using var overlayImage = ImageUtils.GenerateItemCardOverlay(imageInfo.Width, typeIcon);
        canvas.DrawBitmap(overlayImage, new SKPoint(0, imageInfo.Height - overlayImage.Height));

        using var rarityStripe = ImageUtils.GenerateRarityStripe(imageInfo.Width, SKColor.Parse(lockerItem.RarityColor));
        canvas.DrawBitmap(rarityStripe, new SKPoint(0, imageInfo.Height - overlayImage.Height - rarityStripe.Height + 5));
        // TODO: Fix Transparency issues

        var fortniteFont = await _assets.GetFont("Assets/Fonts/Fortnite.ttf"); // don't dispose

        using var namePaint = new SKPaint();
        namePaint.IsAntialias = true;
        namePaint.TextSize = 18.0f;
        namePaint.Color = SKColors.White;
        namePaint.Typeface = fortniteFont;
        namePaint.TextAlign = SKTextAlign.Center;

        var entryNameTextBounds = new SKRect();
        namePaint.MeasureText(lockerItem.Name, ref entryNameTextBounds);
        canvas.DrawText(lockerItem.Name, (float)bitmap.Width / 2, bitmap.Height - 59 + entryNameTextBounds.Height, namePaint);

        using var descriptionPaint = new SKPaint();
        descriptionPaint.IsAntialias = true;
        descriptionPaint.TextSize = 15.0f;
        descriptionPaint.Color = SKColor.Parse(lockerItem.RarityColor);
        descriptionPaint.Typeface = fortniteFont;
        descriptionPaint.TextAlign = SKTextAlign.Center;

        descriptionPaint.MeasureText(lockerItem.Description, ref entryNameTextBounds);
        canvas.DrawText(lockerItem.Description, (float)bitmap.Width / 2, bitmap.Height - 42 + entryNameTextBounds.Height, descriptionPaint);

        using var sourcePaint = new SKPaint();
        sourcePaint.IsAntialias = true;
        sourcePaint.TextSize = 15.0f;
        sourcePaint.Color = SKColors.White;
        sourcePaint.Typeface = fortniteFont;
        sourcePaint.TextAlign = SKTextAlign.Right;

        var fontOffset = lockerItem.SourceType == SourceType.Other ? 10 : 42;

        sourcePaint.MeasureText(lockerItem.Source, ref entryNameTextBounds);
        canvas.DrawText(lockerItem.Source, bitmap.Width - fontOffset, bitmap.Height - entryNameTextBounds.Height + 8, sourcePaint);

        return bitmap;
    }

    private async Task<SKBitmap> GenerateFooter(float resizeFactor)
    {
        var poppinsFont = await _assets.GetFont("Assets/Fonts/Poppins.ttf"); // don't dispose

        using var textPaint = new SKPaint();
        textPaint.IsAntialias = true;
        textPaint.TextSize = 40.0f * resizeFactor;
        textPaint.Color = SKColors.White;
        textPaint.Typeface = poppinsFont;

        //var text = "EasyFnStats.com".ToUpper();
        const string text = "EASYFNSTATS.COM";

        var textBounds = new SKRect();
        textPaint.MeasureText(text, ref textBounds);

        var imageInfo = new SKImageInfo((int) ((50 + 10 + 5 + 10) * resizeFactor + textBounds.Width), (int) (50 * resizeFactor));
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        
        var logoBitmap = await _assets.GetBitmap("Assets/Images/Logo.png");  // don't dispose
        var logoBitmapResize = logoBitmap!.Resize(new SKImageInfo(imageInfo.Height, imageInfo.Height), SKFilterQuality.High);
        canvas.DrawBitmap(logoBitmapResize, new SKPoint(0, 0));

        var splitR = 3 * resizeFactor;

        using var splitPaint = new SKPaint();
        splitPaint.IsAntialias = true;
        splitPaint.Color = SKColors.White;
        canvas.DrawRoundRect((50 + 10) * resizeFactor, (imageInfo.Height - 40 * resizeFactor) / 2, 5 * resizeFactor, 40 * resizeFactor, splitR, splitR, splitPaint);

        canvas.DrawText(text, (50 + 10 + 5 + 10) * resizeFactor, (imageInfo.Height + textBounds.Height) / 2, textPaint);

        return bitmap;
    }
}
