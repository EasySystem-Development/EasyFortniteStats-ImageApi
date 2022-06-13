using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace EasyFortniteStats_ImageApi.Controllers;

public class ShopImageController : ControllerBase
{
    [HttpPost("shop/template")]
    public void Post([FromBody] Shop shop)
    {
        (var shopLocationData, var bitmap) = GenerateTemplate(shop);
        GenerateLocaleTemplate(shop, bitmap, shopLocationData);
        bitmap.Dispose();
    }

    private void GenerateLocaleTemplate(Shop shop, SKBitmap templateBitmap, ShopSectionLocationData[] shopSectionLocationData)
    {
        var imageInfo = new SKImageInfo(templateBitmap.Width, templateBitmap.Height);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear();
        
        canvas.DrawBitmap(templateBitmap, new SKPoint());

        using var fortniteFont = SKTypeface.FromFile(@"Assets/Fonts/Fortnite.ttf");

        // Drawing the shop title
        int shopTitleWidth;
        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;
            paint.TextSize = 100.0f;
            paint.Color = SKColors.White;
            paint.Typeface = fortniteFont;
            
            SKRect textBounds = new SKRect();
            paint.MeasureText(shop.Title, ref textBounds);
            shopTitleWidth = (int)textBounds.Width;

            canvas.DrawText(shop.Title, 50, 50 + textBounds.Height, paint);
        }
        
        // Drawing the date
        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;
            paint.TextSize = 20.0f;
            paint.Color = SKColors.White;
            paint.Typeface = fortniteFont;
            
            SKRect textBounds = new SKRect();
            paint.MeasureText(shop.Title, ref textBounds);

            var textPoint = new SKPoint(
                Math.Max(50, (int) (50 + (shopTitleWidth - textBounds.Width) / 2)), 
                150 + textBounds.Height);
            canvas.DrawText(shop.Date, textPoint, paint);
        }

        foreach (var sectionLocationData in shopSectionLocationData)
        {
            var shopSection = shop.Sections.FirstOrDefault(x => x.Id == sectionLocationData.Id);
            if (sectionLocationData.Name != null)
            {
                var sectionName = shop.Sections.FirstOrDefault(x => x.Id == sectionLocationData.Id)?.Name;
                using (var paint = new SKPaint())
                {
                    paint.IsAntialias = true;
                    paint.TextSize = 25.0f;
                    paint.Color = SKColors.White;
                    paint.Typeface = fortniteFont;
                    
                    SKRect textBounds = new SKRect();
                    paint.MeasureText(shop.Title, ref textBounds);

                    canvas.DrawText(shopSection?.Name, new SKPoint(sectionLocationData.Name.X, sectionLocationData.Name.Y + textBounds.Height), paint);
                }
            }

            foreach (var entryLocationData in sectionLocationData.Entries)
            {
                var shopEntry = shopSection?.Entries.FirstOrDefault(x => x.Id == entryLocationData.Id);
                
                using (var paint = new SKPaint())
                {
                    paint.IsAntialias = true;
                    paint.TextSize = 13.0f;
                    paint.Color = SKColors.White;
                    paint.Typeface = fortniteFont;
                    paint.TextAlign = SKTextAlign.Center;
            
                    var textBounds = new SKRect();
                    paint.MeasureText(shop.Title, ref textBounds);

                    var textPoint = new SKPoint(
                        entryLocationData.Name.X + ((int)entryLocationData.Name.Width - (int)textBounds.Width) / 2, 
                        entryLocationData.Name.Y + textBounds.Height);
                    canvas.DrawText(shopEntry?.Name, textPoint, paint);
                }
            }
        }
        
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var mStream = System.IO.File.OpenWrite(Path.Combine(".", "shop.png"));
        {
            data.SaveTo(mStream);
        }
        
    }

    private (ShopSectionLocationData[], SKBitmap) GenerateTemplate(Shop shop)
    {
        var sectionWidths = shop.Sections.Select(x => (int) x.Entries.Sum(y => y.Size)).ToArray();
        var imageInfo = new SKImageInfo(50 + sectionWidths.Max() * 191 + (sectionWidths.Max() - 1) * 15 + 50,
            175 + (50 + 330) * sectionWidths.Length + 50);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        var shopLocationData = new ShopSectionLocationData[shop.Sections.Length];
        for (int i = 0; i < shop.Sections.Length; i++)
        {
            var section = shop.Sections[i];
            var sectionImageInfo = new SKImageInfo(sectionWidths[i] * 191 + (sectionWidths[i] - 1) * 15, 330);
            using var sectionBitmap = new SKBitmap(sectionImageInfo);
            using var sectionCanvas = new SKCanvas(sectionBitmap);

            int sectionX = 50, sectionY = 50 + 125 + 50 + (50 + 330) * i;

            var shopEntryData = new List<ShopEntryLocationData>();
            var j = 0.0;
            foreach (var entry in section.Entries)
            {
                int entryX = (int) j * 191 + (int) j * 15, entryY = Math.Floor(j).Equals(j) ? 0 : 157 + 16;
                using var itemCardBitmap = GenerateItemCard(entry);
                sectionCanvas.DrawBitmap(itemCardBitmap, new SKPoint(entryX, entryY));

                j += entry.Size;

                var nameLocationData = new ShopLocationDataEntry(sectionX + entryX,
                    sectionY + entryY + itemCardBitmap.Height - 35, itemCardBitmap.Width);
                var priceLocationData = new ShopLocationDataEntry(sectionX + entryX + itemCardBitmap.Width - 27,
                    sectionY + entryY + itemCardBitmap.Height - 13);
                ShopLocationDataEntry? bannerLocationData = null;
                if (entry.BannerText != null)
                    bannerLocationData = new ShopLocationDataEntry(sectionX + entryX - 5, sectionY + entryY - 5);
                shopEntryData.Add(new ShopEntryLocationData(entry.Id, nameLocationData, priceLocationData,
                    bannerLocationData));
            }
            
            ShopLocationDataEntry? sectionNameLocationData = null;
            if (section.Name != null)
                sectionNameLocationData = new ShopLocationDataEntry(sectionX + 20, sectionY - 35);

            shopLocationData[i] = new ShopSectionLocationData(section.Id, sectionNameLocationData, shopEntryData.ToArray());

            canvas.DrawBitmap(sectionBitmap, new SKPoint(sectionX, sectionY));
        }
        return (shopLocationData, bitmap);
    }

    private SKBitmap GenerateItemCard(ShopEntry shopEntry)
    {
        var imageInfo = new SKImageInfo(
            (int) Math.Ceiling(shopEntry.Size) * 191 + ((int) Math.Ceiling(shopEntry.Size) - 1) * 15,
            Math.Floor(shopEntry.Size).Equals(shopEntry.Size) ? 330 : 157);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);

        var response = new HttpClient().GetAsync(shopEntry.ImageUrl).Result;
        using var imageBitmap = SKBitmap.Decode(response.Content.ReadAsByteArrayAsync().Result);
        var imageResize = shopEntry.Size.Equals(1.0) ? 300 : imageInfo.Width;
        using var resizedImageBitmap =
            imageBitmap.Resize(new SKImageInfo(imageResize, imageResize), SKFilterQuality.Medium);

        if (shopEntry.Size.Equals(1.0))
        {
            // Crop resized bitmap to copXY
            var cropX = (resizedImageBitmap.Width - imageInfo.Width) / 2;
            var cropRect = new SKRect(cropX, 0, cropX + imageInfo.Width, resizedImageBitmap.Height);
            canvas.DrawBitmap(resizedImageBitmap, cropRect,
                new SKRect(0, 0, imageInfo.Width, resizedImageBitmap.Height));
        }
        else canvas.DrawBitmap(resizedImageBitmap, new SKPoint(0, 0));

        using var overlayImage = GenerateItemCardOverlay(imageInfo.Width, true);
        canvas.DrawBitmap(overlayImage, new SKPoint(0, imageInfo.Height - overlayImage.Height));

        using var rarityStripe = GenerateRarityStripe(imageInfo.Width, shopEntry.RarityColor);
        canvas.DrawBitmap(rarityStripe,
            new SKPoint(0, imageInfo.Height - overlayImage.Height - rarityStripe.Height + 4));

        return bitmap;
    }

    private SKBitmap GenerateItemCardOverlay(int width, bool vbucksIcon = false)
    {
        var imageInfo = new SKImageInfo(width, 44);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;
            paint.Color = new SKColor(14, 14, 14);
            paint.Style = SKPaintStyle.Fill;

            using var path = new SKPath();
            path.MoveTo(0, imageInfo.Height - 16);
            path.LineTo(imageInfo.Width, imageInfo.Height - 17);
            path.LineTo(imageInfo.Width, imageInfo.Height);
            path.LineTo(0, imageInfo.Height);
            path.Close();

            canvas.DrawPath(path, paint);
        }

        if (vbucksIcon)
        {
            using var vbucksBitmap = SKBitmap.Decode(@"Assets/Images/Shop/vbucks_icon.png"); // TODO: Cache
            using var rotatedVbucksBitmap = RotateBitmap(vbucksBitmap, -20);
            using var resizedVBucksBitmap = rotatedVbucksBitmap.Resize(new SKImageInfo(32, 32), SKFilterQuality.Medium);

            canvas.DrawBitmap(resizedVBucksBitmap, new SKPoint(imageInfo.Width - 31, imageInfo.Height - 23));
        }

        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;
            paint.Color = new SKColor(30, 30, 30);
            paint.Style = SKPaintStyle.Fill;

            using var path = new SKPath();
            path.MoveTo(0, imageInfo.Height - 40);
            path.LineTo(imageInfo.Width, imageInfo.Height - 44);
            path.LineTo(imageInfo.Width, imageInfo.Height - 17);
            path.LineTo(0, imageInfo.Height - 16);
            path.Close();

            canvas.DrawPath(path, paint);
        }

        return bitmap;
    }

    private SKBitmap GenerateRarityStripe(int width, string rarityColor)
    {
        var imageInfo = new SKImageInfo(width, 9);
        var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint();
        {
            paint.IsAntialias = true;
            paint.Color = SKColor.Parse(rarityColor);
            paint.Style = SKPaintStyle.Fill;

            using var path = new SKPath();
            path.MoveTo(0, imageInfo.Height - 3);
            path.LineTo(imageInfo.Width, 0);
            path.LineTo(imageInfo.Width, imageInfo.Height - 4);
            path.LineTo(0, imageInfo.Height);
            path.Close();

            canvas.DrawPath(path, paint);
        }
        return bitmap;
    }

    private SKBitmap RotateBitmap(SKBitmap bitmap, double angle)
    {
        double radians = Math.PI * angle / 180;
        float sine = (float) Math.Abs(Math.Sin(radians));
        float cosine = (float) Math.Abs(Math.Cos(radians));
        int originalWidth = bitmap.Width, originalHeight = bitmap.Height;
        int rotatedWidth = (int) (cosine * originalWidth + sine * originalHeight);
        int rotatedHeight = (int) (cosine * originalHeight + sine * originalWidth);

        var rotatedBitmap = new SKBitmap(rotatedWidth, rotatedHeight);
        using (var rotatedCanvas = new SKCanvas(rotatedBitmap))
        {
            rotatedCanvas.Clear();
            rotatedCanvas.Translate(rotatedWidth / 2, rotatedHeight / 2);
            rotatedCanvas.RotateDegrees((float) -angle);
            rotatedCanvas.Translate(-originalWidth / 2, -originalHeight / 2);
            rotatedCanvas.DrawBitmap(bitmap, new SKPoint());
        }

        return rotatedBitmap;
    }
}