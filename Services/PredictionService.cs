using System.Collections.Generic;

namespace predskoz.Services;

public class PredictionService
{
    private readonly Dictionary<string, List<string>> _predictionsByType;
    
    public PredictionService()
    {
        _predictionsByType = new Dictionary<string, List<string>>
        {
            ["general"] = new List<string>
            {
                "Судьба благословляет вас. Ожидайте приятных сюрпризов.",
                "Ладонь предсказывает осуществление давней мечты. Время пришло.",
                "Ваша ладонь говорит о независимости и свободолюбии.",
                "Ладонь говорит о вашей мудрости в отношениях.",
                "Линии руки говорят о вашей интуиции. Чутьё не подведёт.",
                "Ваша рука говорит о мудрости и рассудительности.",
                "Вас ждёт успех в начинаниях. Доверьтесь судьбе.",
                "Линии предсказывают скорое путешествие."
            }
        };
    }
    
    public string GetRandomPrediction()
    {
        var random = new System.Random();
        var generalPredictions = _predictionsByType["general"];
        return generalPredictions[random.Next(generalPredictions.Count)];
    }
}