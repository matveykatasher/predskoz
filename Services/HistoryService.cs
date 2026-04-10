using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using predskoz.Models;

namespace predskoz.Services;

public class HistoryService
{
    private readonly string _historyFilePath;
    private List<PredictionHistory> _history = new(); // Исправлено: инициализация
    
    public HistoryService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "predskoz");
        
        if (!Directory.Exists(appDataPath))
            Directory.CreateDirectory(appDataPath);
            
        _historyFilePath = Path.Combine(appDataPath, "history.json");
        LoadHistory();
    }
    
    private void LoadHistory()
    {
        if (File.Exists(_historyFilePath))
        {
            var json = File.ReadAllText(_historyFilePath);
            _history = JsonSerializer.Deserialize<List<PredictionHistory>>(json) ?? new List<PredictionHistory>();
        }
        else
        {
            _history = new List<PredictionHistory>();
        }
    }
    
    private void SaveHistory()
    {
        var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_historyFilePath, json);
    }
    
    public void AddPrediction(string imagePath, string prediction)
    {
        var thumbnailPath = CreateThumbnail(imagePath);
        
        _history.Insert(0, new PredictionHistory
        {
            ImagePath = imagePath,
            Prediction = prediction,
            Timestamp = DateTime.Now,
            ThumbnailPath = thumbnailPath
        });
        
        if (_history.Count > 50)
            _history = _history.Take(50).ToList();
            
        SaveHistory();
    }
    
    private string CreateThumbnail(string imagePath)
    {
        var thumbDir = Path.Combine(Path.GetDirectoryName(_historyFilePath) ?? "", "thumbnails");
        if (!Directory.Exists(thumbDir))
            Directory.CreateDirectory(thumbDir);
            
        var thumbPath = Path.Combine(thumbDir, $"{Guid.NewGuid()}.jpg");
        
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(imagePath);
            image.Mutate(x => x.Resize(100, 75));
            // Исправлено: добавлен encoder
            image.Save(thumbPath, new JpegEncoder());
        }
        catch
        {
            return imagePath;
        }
        
        return thumbPath;
    }
    
    public List<PredictionHistory> GetHistory() => _history.ToList();
    
    public void ClearHistory()
    {
        _history.Clear();
        SaveHistory();
    }
}