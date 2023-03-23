using System.Text.Json.Serialization;
using SkiaSharp;

namespace EasyFortniteStats_ImageApi.Models;

public class Shop
{
    public string Locale { get; set; }
    public string Date { get; set; }
    public string Title { get; set; }
    public string CreatorCodeTitle { get; set; }
    public string? CreatorCode { get; set; }
    public string? BackgroundImagePath { get; set; }
    public ShopSection[] Sections { get; set; } 
    public bool? NewShop { get; set; }
}

public class ShopSection
{
    public string Id { get; set; }
    public string? Name { get; set; }
    public ShopEntry[] Entries { get; set; }
}

public class ShopEntry
{
    public string Id { get; set; }
    public int RegularPrice { get; set; }
    public int FinalPrice { get; set; }
    public string? BannerText { get; set; }
    public string[]? BannerColor { get; set; }
    public float Size { get; set; }
    public string Name { get; set; }
    public string RarityColor { get; set; }
    public string? ImageUrl { get; set; }
    public string FallbackImageUrl { get; set; }
    public bool Special { get; set; }

    [JsonIgnore] public SKBitmap? Image { get; set; }
}

public class ShopSectionLocationData
{
    public ShopSectionLocationData(string id, ShopLocationDataEntry? name, ShopEntryLocationData[] entries)
    {
        Id = id;
        Name = name;
        Entries = entries;
    }
    
    public string Id { get; }
    public ShopLocationDataEntry? Name { get; }
    public ShopEntryLocationData[] Entries { get; }
} 

public class ShopEntryLocationData
{
    public ShopEntryLocationData(string id, ShopLocationDataEntry name, ShopLocationDataEntry price, ShopLocationDataEntry? banner)
    {
        Id = id;
        Name = name;
        Price = price;
        Banner = banner;
    }
    
    public string Id { get; }
    public ShopLocationDataEntry Name { get; }
    public ShopLocationDataEntry Price { get; }
    public ShopLocationDataEntry? Banner { get; }
}

public class ShopLocationDataEntry
{
    public ShopLocationDataEntry(int x, int y)
    {
        X = x;
        Y = y;
    }
    
    public ShopLocationDataEntry(int x, int y, int width)
    {
        X = x;
        Y = y;
        Width = width;
    }
    
    public int X { get; }
    public int Y { get; }
    public int? Width { get; }
}