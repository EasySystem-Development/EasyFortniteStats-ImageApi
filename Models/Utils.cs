namespace EasyFortniteStats_ImageApi;

public class ProgressBar
{
    public float Progress { get; set; }
    
    public string Percentage { get; set; }
    public string[] GradientColors { get; set; }
}

public class Drop
{
    public IFormFile MapImage { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}