using System.Text.Json.Serialization;

using SkiaSharp;

namespace EasyFortniteStats_ImageApi.Models;

public class Locker
{
    public string RequestId { get; set; }
    public string Locale { get; set; }
    public string PlayerName { get; set; }
    public string UserName { get; set; }
    public LockerItem[] Items { get; set; }
}

public class LockerItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Rarity { get; set; }
    public string RarityColor { get; set; }
    public string? ImageUrl { get; set; }
    public SourceType SourceType { get; set; }
    public string Source { get; set; }

    [JsonIgnore] public SKBitmap? Image { get; set; }
}

public enum SourceType
{
    VBucks = 1,
    BattlePassPaid = 2,
    BattlePassFree = 3,
    Other = 0
}