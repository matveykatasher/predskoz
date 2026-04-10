using System;

namespace predskoz.Models;

public class PredictionHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ImagePath { get; set; } = string.Empty;
    public string Prediction { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ThumbnailPath { get; set; } = string.Empty;
}