using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using predskoz.Models;

namespace predskoz.Services;

public class PalmReaderService
{
    private readonly Random _random = new Random();
    private readonly string _assetsPath;
    
    public PalmReaderService()
    {
        _assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
        if (!Directory.Exists(_assetsPath))
        {
            Directory.CreateDirectory(_assetsPath);
        }
    }
    
    public PalmAnalysis AnalyzePalm(string imagePath, SelectionRectangle? selectionArea = null)
    {
        using var image = Image.Load<Rgba32>(imagePath);
        image.Mutate(x => x.Resize(800, 600));
        
        if (selectionArea.HasValue)
        {
            var rect = selectionArea.Value;
            var cropRectangle = new SixLabors.ImageSharp.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
            image.Mutate(x => x.Crop(cropRectangle));
        }
        
        // Улучшаем контраст для лучшего обнаружения линий
        image.Mutate(x => x.Contrast(2.5f));
        image.Mutate(x => x.Brightness(1.2f));
        
        var contours = DetectContours(image);
        
        var lifeLineQuality = AnalyzeLifeLine(contours);
        var heartLineQuality = AnalyzeHeartLine(contours);
        var headLineQuality = AnalyzeHeadLine(contours);
        
        var prediction = GeneratePrediction(lifeLineQuality, heartLineQuality, headLineQuality);
        
        return new PalmAnalysis
        {
            LifeLine = GetLifeLineDescription(lifeLineQuality),
            HeartLine = GetHeartLineDescription(heartLineQuality),
            HeadLine = GetHeadLineDescription(headLineQuality),
            OverallPrediction = prediction,
            Confidence = CalculateConfidence(lifeLineQuality, heartLineQuality, headLineQuality)
        };
    }
    
    private byte[,] DetectContours(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var grayImage = new byte[width, height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = image[x, y];
                grayImage[x, y] = (byte)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
            }
        }
        
        // Применяем размытие для уменьшения шума
        grayImage = ApplyGaussianBlur(grayImage, width, height);
        
        var sobelX = new int[,] { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
        var sobelY = new int[,] { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };
        
        var gradient = new double[width, height];
        double maxGradient = 0;
        
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                double gx = 0, gy = 0;
                
                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        var pixel = grayImage[x + kx, y + ky];
                        gx += pixel * sobelX[ky + 1, kx + 1];
                        gy += pixel * sobelY[ky + 1, kx + 1];
                    }
                }
                
                gradient[x, y] = Math.Sqrt(gx * gx + gy * gy);
                if (gradient[x, y] > maxGradient)
                    maxGradient = gradient[x, y];
            }
        }
        
        var contours = new byte[width, height];
        double threshold = maxGradient * 0.08;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                contours[x, y] = gradient[x, y] > threshold ? (byte)255 : (byte)0;
            }
        }
        
        return contours;
    }
    
    private byte[,] ApplyGaussianBlur(byte[,] image, int width, int height)
    {
        var result = new byte[width, height];
        var kernel = new double[,]
        {
            { 1, 2, 1 },
            { 2, 4, 2 },
            { 1, 2, 1 }
        };
        double kernelSum = 16.0;
        
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                double sum = 0;
                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        sum += image[x + kx, y + ky] * kernel[ky + 1, kx + 1];
                    }
                }
                result[x, y] = (byte)(sum / kernelSum);
            }
        }
        
        return result;
    }
    
    private double AnalyzeLifeLine(byte[,] contours)
    {
        int height = contours.GetLength(1);
        int width = contours.GetLength(0);
        int linePoints = 0;
        
        for (int y = height / 2; y < height; y++)
        {
            for (int x = width / 4; x < width / 2; x++)
            {
                if (contours[x, y] > 0)
                    linePoints++;
            }
        }
        
        return Math.Min(1.0, linePoints / 400.0);
    }
    
    private double AnalyzeHeartLine(byte[,] contours)
    {
        int height = contours.GetLength(1);
        int width = contours.GetLength(0);
        int linePoints = 0;
        
        for (int y = height / 4; y < height / 2; y++)
        {
            for (int x = width / 4; x < 3 * width / 4; x++)
            {
                if (contours[x, y] > 0)
                    linePoints++;
            }
        }
        
        return Math.Min(1.0, linePoints / 500.0);
    }
    
    private double AnalyzeHeadLine(byte[,] contours)
    {
        int height = contours.GetLength(1);
        int width = contours.GetLength(0);
        int linePoints = 0;
        
        for (int y = height / 3; y < 2 * height / 3; y++)
        {
            for (int x = width / 4; x < 3 * width / 4; x++)
            {
                if (contours[x, y] > 0)
                    linePoints++;
            }
        }
        
        return Math.Min(1.0, linePoints / 450.0);
    }
    
    private string GetLifeLineDescription(double quality)
    {
        if (quality > 0.7)
            return "❤️ Линия жизни глубокая и чёткая - вы обладаете сильной жизненной энергией и крепким здоровьем.";
        if (quality > 0.4)
            return "💚 Линия жизни средней глубины - у вас хороший жизненный потенциал, но нужно беречь силы.";
        return "💛 Линия жизни слабо выражена - вам стоит больше внимания уделять здоровью и отдыху.";
    }
    
    private string GetHeartLineDescription(double quality)
    {
        if (quality > 0.7)
            return "💖 Линия сердца выражена ярко - вы способны на глубокие чувства и искреннюю любовь.";
        if (quality > 0.4)
            return "💗 Линия сердца чёткая - вы цените отношения и умеете проявлять заботу.";
        return "💔 Линия сердца слабая - вам стоит больше доверять своим чувствам.";
    }
    
    private string GetHeadLineDescription(double quality)
    {
        if (quality > 0.7)
            return "🧠 Линия ума чёткая - вы принимаете взвешенные решения и обладаете острым умом.";
        if (quality > 0.4)
            return "📚 Линия ума хорошо выражена - вы практичны и находите решения в сложных ситуациях.";
        return "🤔 Линия ума размыта - иногда вам не хватает концентрации, развивайте внимательность.";
    }
    
    private string GeneratePrediction(double life, double heart, double head)
    {
        var predictions = new[]
        {
            "🌟 Судьба благословляет вас. Ожидайте приятных сюрпризов и встречи с важными людьми.",
            "🔮 Ладонь предсказывает осуществление давней мечты. Время пришло.",
            "🦋 Ваша ладонь говорит о независимости и свободолюбии. Следуйте своей мечте.",
            "💫 Ладонь говорит о вашей мудрости в отношениях. Вы знаете, что действительно важно.",
            "✨ Линии руки говорят о вашей интуиции. Чутьё вас не подведёт.",
            "🎯 Ваша рука говорит о мудрости и рассудительности. Принимайте решения с уверенностью.",
            "🚀 Вас ждёт успех в начинаниях. Доверьтесь своей судьбе.",
            "✈️ Линии предсказывают скорое путешествие, которое изменит вашу жизнь."
        };
        
        var score = (life + heart + head) / 3;
        var index = (int)(score * predictions.Length);
        index = Math.Clamp(index, 0, predictions.Length - 1);
        
        return predictions[index];
    }
    
    private double CalculateConfidence(double life, double heart, double head)
    {
        return (life + heart + head) / 3;
    }
    
    public string DrawLinesOnImage(string imagePath, int targetWidth = 450, int targetHeight = 450, SelectionRectangle? selectionArea = null)
    {
        try
        {
            using var image = Image.Load<Rgba32>(imagePath);
            
            if (selectionArea.HasValue)
            {
                var rect = selectionArea.Value;
                var scaleX = (double)image.Width / targetWidth;
                var scaleY = (double)image.Height / targetHeight;
                
                var cropRect = new SixLabors.ImageSharp.Rectangle(
                    (int)(rect.X * scaleX),
                    (int)(rect.Y * scaleY),
                    (int)(rect.Width * scaleX),
                    (int)(rect.Height * scaleY)
                );
                
                image.Mutate(x => x.Crop(cropRect));
            }
            
            image.Mutate(x => x.Resize(targetWidth, targetHeight));
            
            // Усиливаем контраст для лучшей видимости линий
            image.Mutate(x => x.Contrast(3.0f));
            image.Mutate(x => x.Brightness(1.3f));
            
            var contours = DetectContours(image);
            
            DrawHandContour(image, contours);
            DrawPalmLines(image, contours);
            
            var outputFileName = $"processed_{Guid.NewGuid()}.png";
            var outputPath = Path.Combine(_assetsPath, outputFileName);
            
            image.SaveAsPng(outputPath);
            
            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error drawing lines: {ex.Message}");
            return CreateDemoImage(imagePath, targetWidth, targetHeight);
        }
    }
    
    private string CreateDemoImage(string imagePath, int width, int height)
    {
        try
        {
            using var image = Image.Load<Rgba32>(imagePath);
            image.Mutate(x => x.Resize(width, height));
            
            image.Mutate(x => x.Contrast(2.5f));
            
            var green = new SixLabors.ImageSharp.Color(new Rgba32(0, 255, 0, 200));
            var red = new SixLabors.ImageSharp.Color(new Rgba32(255, 0, 0, 220));
            
            var lifeLinePoints = new List<PointF>();
            int centerX = width / 3;
            int centerY = height / 2;
            for (double angle = 0; angle < Math.PI / 2; angle += 0.03)
            {
                float x = centerX + (float)(width / 4 * Math.Cos(angle));
                float y = centerY + (float)(height / 3 * Math.Sin(angle));
                lifeLinePoints.Add(new PointF(x, y));
            }
            
            var heartLinePoints = new List<PointF>();
            for (int x = width / 4; x < 3 * width / 4; x += 2)
            {
                float y = height / 3 + (float)(12 * Math.Sin(x / 40.0));
                heartLinePoints.Add(new PointF(x, y));
            }
            
            var headLinePoints = new List<PointF>();
            for (int x = width / 4; x < 3 * width / 4; x += 2)
            {
                float y = height / 2 + (float)(8 * Math.Cos(x / 35.0));
                headLinePoints.Add(new PointF(x, y));
            }
            
            var fateLinePoints = new List<PointF>();
            for (int y = height / 4; y < 3 * height / 4; y += 2)
            {
                float x = width / 2 + (float)(10 * Math.Sin(y / 25.0));
                fateLinePoints.Add(new PointF(x, y));
            }
            
            image.Mutate(ctx =>
            {
                DrawPolyline(ctx, red, 4, lifeLinePoints.ToArray());
                DrawPolyline(ctx, red, 4, heartLinePoints.ToArray());
                DrawPolyline(ctx, red, 4, headLinePoints.ToArray());
                DrawPolyline(ctx, red, 4, fateLinePoints.ToArray());
            });
            
            var outputPath = Path.Combine(_assetsPath, $"demo_{Guid.NewGuid()}.png");
            image.SaveAsPng(outputPath);
            
            return outputPath;
        }
        catch
        {
            return imagePath;
        }
    }
    
    private void DrawHandContour(Image<Rgba32> image, byte[,] contours)
    {
        var green = new SixLabors.ImageSharp.Color(new Rgba32(0, 255, 0, 180));
        int width = contours.GetLength(0);
        int height = contours.GetLength(1);
        
        image.Mutate(ctx =>
        {
            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    if (contours[x, y] > 0)
                    {
                        ctx.DrawLine(green, 3, new PointF(x, y), new PointF(x + 1, y + 1));
                    }
                }
            }
        });
    }
    
    private void DrawPalmLines(Image<Rgba32> image, byte[,] contours)
    {
        var red = new SixLabors.ImageSharp.Color(new Rgba32(255, 0, 0, 220));
        int width = contours.GetLength(0);
        int height = contours.GetLength(1);
        
        var linePoints = new List<PointF>();
        
        for (int y = height / 5; y < height * 4 / 5; y += 2)
        {
            int consecutivePoints = 0;
            int startX = -1;
            
            for (int x = width / 5; x < width * 4 / 5; x++)
            {
                if (contours[x, y] > 0)
                {
                    if (consecutivePoints == 0) startX = x;
                    consecutivePoints++;
                }
                else
                {
                    if (consecutivePoints > 3)
                    {
                        for (int i = 0; i < consecutivePoints; i += 2)
                        {
                            linePoints.Add(new PointF(startX + i, y));
                        }
                    }
                    consecutivePoints = 0;
                }
            }
        }
        
        image.Mutate(ctx =>
        {
            foreach (var point in linePoints)
            {
                ctx.DrawLine(red, 4, point, new PointF(point.X + 1, point.Y + 1));
            }
        });
        
        if (linePoints.Count < 30)
        {
            DrawDemoLinesOnImage(image, width, height);
        }
    }
    
    private void DrawDemoLinesOnImage(Image<Rgba32> image, int width, int height)
    {
        var red = new SixLabors.ImageSharp.Color(new Rgba32(255, 0, 0, 220));
        
        var lifeLinePoints = new List<PointF>();
        int centerX = width / 3;
        int centerY = height / 2;
        for (double angle = 0; angle < Math.PI / 2; angle += 0.03)
        {
            float x = centerX + (float)(width / 4 * Math.Cos(angle));
            float y = centerY + (float)(height / 3 * Math.Sin(angle));
            lifeLinePoints.Add(new PointF(x, y));
        }
        
        var heartLinePoints = new List<PointF>();
        for (int x = width / 4; x < 3 * width / 4; x += 2)
        {
            float y = height / 3 + (float)(12 * Math.Sin(x / 40.0));
            heartLinePoints.Add(new PointF(x, y));
        }
        
        var headLinePoints = new List<PointF>();
        for (int x = width / 4; x < 3 * width / 4; x += 2)
        {
            float y = height / 2 + (float)(8 * Math.Cos(x / 35.0));
            headLinePoints.Add(new PointF(x, y));
        }
        
        var fateLinePoints = new List<PointF>();
        for (int y = height / 4; y < 3 * height / 4; y += 2)
        {
            float x = width / 2 + (float)(10 * Math.Sin(y / 25.0));
            fateLinePoints.Add(new PointF(x, y));
        }
        
        image.Mutate(ctx =>
        {
            DrawPolyline(ctx, red, 4, lifeLinePoints.ToArray());
            DrawPolyline(ctx, red, 4, heartLinePoints.ToArray());
            DrawPolyline(ctx, red, 4, headLinePoints.ToArray());
            DrawPolyline(ctx, red, 4, fateLinePoints.ToArray());
        });
    }
    
    private void DrawPolyline(IImageProcessingContext ctx, SixLabors.ImageSharp.Color color, float thickness, PointF[] points)
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            ctx.DrawLine(color, thickness, points[i], points[i + 1]);
        }
    }
}

public struct SelectionRectangle
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    public SelectionRectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}