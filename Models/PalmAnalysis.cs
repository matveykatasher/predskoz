namespace predskoz.Models;

public class PalmAnalysis
{
    public string LifeLine { get; set; } = string.Empty;
    public string HeartLine { get; set; } = string.Empty;
    public string HeadLine { get; set; } = string.Empty;
    public string OverallPrediction { get; set; } = string.Empty;
    public double Confidence { get; set; }
}