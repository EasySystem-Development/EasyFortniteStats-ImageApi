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

    public AccountImageController(IMemoryCache cache, IHttpClientFactory clientFactory, NamedLock namedLock, SharedAssets assets)
    {
        _cache = cache;
        _clientFactory = clientFactory;
        _namedLock = namedLock;
        _assets = assets;
    }

    [HttpPost]
    public async Task<IActionResult> Post(Locker locker)
    {
        await _namedLock.WaitAsync($"locker_{locker.RequestId}");
        var itemsCards = await GenerateItemCards(locker);

        using var lockerBitmap = await GenerateImage(locker, itemsCards);
        _namedLock.Release($"locker_{locker.RequestId}");
        
        var data = lockerBitmap.Encode(SKEncodedImageFormat.Png, 100);
        return File(data.AsStream(true), "image/png");
    }

    private async Task<SKBitmap> GenerateImage(Locker locker, IReadOnlyDictionary<string, SKBitmap> itemCards)
    {
        // Calculate rows and columns based on locker.Items count
        // Columns and Rows should be equal
        // min value is 5
        var columns = new[] {(int) Math.Ceiling(Math.Sqrt(locker.Items.Length)), 5}.Max();
        var rows = locker.Items.Length / columns + (locker.Items.Length % columns == 0 ? 0 : 1);

        var nameFontSize = 64;
        
        var imageInfo = new SKImageInfo(
            50 + 256 * columns + 25 * (columns - 1) + 50, 
            50 + nameFontSize + 25 + rows * 313 + (rows - 1) * 25 + 50);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        var column = 0; var row = 0;
        foreach (var item in locker.Items)
        {
            canvas.DrawBitmap(
                itemCards[item.Id], 
                50 + 256 * column + 25 * (column - 1), 
                50 + nameFontSize + 25 + row * 313 + (row - 1) * 25);
            column++;
            if (column != columns) continue;
            column = 0;
            row++;
        }

        return bitmap;
    }
    
    private async Task<Dictionary<string, SKBitmap>> GenerateItemCards(Locker locker)
    {
        Dictionary<string, SKBitmap> itemCards = new();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount / 2
        };
        await Parallel.ForEachAsync(locker.Items, options, async (item, token) =>
        {
            var itemCard = _cache.Get<SKBitmap?>($"locker_card_image_{item.Id}_{locker.Locale}");
            if (itemCard == null)
            {
                var itemImage = _cache.Get<byte[]?>($"locker_image_{item.Id}");
                if (itemImage == null)
                {
                    var client = _clientFactory.CreateClient();
                    itemImage = await client.GetByteArrayAsync(item.ImageUrl, token);
                    //_cache.Set($"locker_image_{item.Id}", itemImage, TimeSpan.FromDays(1));
                }
                item.Image = SKBitmap.Decode(itemImage);
                //_cache.Set($"locker_card_image_{item.Id}_{locker.Locale}", item.Image);

                itemCard = await GenerateItemCard(item);
            }
            itemCards.Add(item.Id, itemCard);
        });
        return itemCards;
    }

    private async Task<SKBitmap> GenerateItemCard(LockerItem lockerItem)
    {
        var imageInfo = new SKImageInfo( 256, 313);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        
        var rarityBackground = await _assets.GetBitmap($"Assets/Images/Locker/RarityBackgrounds/{lockerItem.Rarity}.png");
        canvas.DrawBitmap(rarityBackground, new SKPoint());
        
        canvas.DrawBitmap(lockerItem.Image, new SKPoint());

        var typeIcon = lockerItem.SourceType != SourceType.Other ? await _assets.GetBitmap($"Assets/Images/Locker/Source/{lockerItem.SourceType}.png") : null;
        using var overlayImage = ImageUtils.GenerateItemCardOverlay(imageInfo.Width, typeIcon);
        canvas.DrawBitmap(overlayImage, new SKPoint(0, imageInfo.Height - overlayImage.Height));
        
        using var rarityStripe = ImageUtils.GenerateRarityStripe(imageInfo.Width, SKColor.Parse(lockerItem.RarityColor));
        canvas.DrawBitmap(rarityStripe, new SKPoint(0, imageInfo.Height - overlayImage.Height - rarityStripe.Height + 5));
        // TODO: Fix Transparency issues
        
        var fortniteFont = await _assets.GetFont(@"Assets/Fonts/Fortnite.ttf"); // don't dispose
        
        using var namePaint = new SKPaint();
        namePaint.IsAntialias = true;
        namePaint.TextSize = 18;
        namePaint.Color = SKColors.White;
        namePaint.Typeface = fortniteFont;
        namePaint.TextAlign = SKTextAlign.Center;
        
        var entryNameTextBounds = new SKRect();
        namePaint.MeasureText(lockerItem.Name, ref entryNameTextBounds);
        
        canvas.DrawText(lockerItem.Name, (float)bitmap.Width / 2, bitmap.Height - 59 + entryNameTextBounds.Height, namePaint);
        
        using var descriptionPaint = new SKPaint();
        descriptionPaint.IsAntialias = true;
        descriptionPaint.TextSize = 15;
        descriptionPaint.Color = SKColor.Parse(lockerItem.RarityColor);
        descriptionPaint.Typeface = fortniteFont;
        descriptionPaint.TextAlign = SKTextAlign.Center;
        
        descriptionPaint.MeasureText(lockerItem.Description, ref entryNameTextBounds);
        
        canvas.DrawText(lockerItem.Description, (float)bitmap.Width / 2, bitmap.Height - 42 + entryNameTextBounds.Height, descriptionPaint);

        return bitmap;
    }
    
}