using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;
using predskoz.Services;
using predskoz.Models;

namespace predskoz;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel(this);
        DataContext = ViewModel;
    }
    
    public MainWindowViewModel ViewModel { get; }
    
    public void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (ViewModel.IsSelecting)
        {
            var position = e.GetPosition(sender as Grid);
            ViewModel.StartSelection(position.X, position.Y);
        }
    }
    
    public void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (ViewModel.IsSelecting && ViewModel.IsDragging)
        {
            var position = e.GetPosition(sender as Grid);
            ViewModel.UpdateSelection(position.X, position.Y);
        }
    }
    
    public void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (ViewModel.IsSelecting && ViewModel.IsDragging)
        {
            ViewModel.EndSelection();
        }
    }
    
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        
        if (ViewModel.IsSelecting)
        {
            if (e.Key == Key.Escape)
            {
                ViewModel.CancelSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ViewModel.ApplySelection();
                e.Handled = true;
            }
        }
    }
}

public class MainWindowViewModel : ReactiveObject
{
    private readonly PalmReaderService _palmReaderService;
    private readonly PredictionService _predictionService;
    private readonly HistoryService _historyService;
    private readonly MainWindow _window;
    
    private Bitmap? _currentImage;
    private string? _originalImagePath;
    private string? _processedImagePath;
    private string _currentPrediction = "✨ Выделите область ладони и нажмите 'Начать гадание' ✨";
    private string _lifeLineAnalysis = "Ожидание анализа...";
    private string _heartLineAnalysis = "Ожидание анализа...";
    private string _headLineAnalysis = "Ожидание анализа...";
    private string _complexityNote = "Загрузите фото и выделите область ладони для точного анализа";
    private string _statusMessage = "Готов к работе. Загрузите фото ладони.";
    private bool _hasImage;
    private bool _isSelecting;
    private bool _isDragging;
    private bool _hasSelection;
    private double _selectionX;
    private double _selectionY;
    private double _selectionWidth;
    private double _selectionHeight;
    private string _selectionInfo = "Область не выделена";
    private double _startX;
    private double _startY;
    private SelectionRectangle? _selectionRect;
    
    public Bitmap? CurrentImage
    {
        get => _currentImage;
        set => this.RaiseAndSetIfChanged(ref _currentImage, value);
    }
    
    public string CurrentPrediction
    {
        get => _currentPrediction;
        set => this.RaiseAndSetIfChanged(ref _currentPrediction, value);
    }
    
    public string LifeLineAnalysis
    {
        get => _lifeLineAnalysis;
        set => this.RaiseAndSetIfChanged(ref _lifeLineAnalysis, value);
    }
    
    public string HeartLineAnalysis
    {
        get => _heartLineAnalysis;
        set => this.RaiseAndSetIfChanged(ref _heartLineAnalysis, value);
    }
    
    public string HeadLineAnalysis
    {
        get => _headLineAnalysis;
        set => this.RaiseAndSetIfChanged(ref _headLineAnalysis, value);
    }
    
    public string ComplexityNote
    {
        get => _complexityNote;
        set => this.RaiseAndSetIfChanged(ref _complexityNote, value);
    }
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }
    
    public bool HasImage
    {
        get => _hasImage;
        set => this.RaiseAndSetIfChanged(ref _hasImage, value);
    }
    
    public bool IsSelecting
    {
        get => _isSelecting;
        set => this.RaiseAndSetIfChanged(ref _isSelecting, value);
    }
    
    public bool IsDragging
    {
        get => _isDragging;
        set => this.RaiseAndSetIfChanged(ref _isDragging, value);
    }
    
    public bool HasSelection
    {
        get => _hasSelection;
        set => this.RaiseAndSetIfChanged(ref _hasSelection, value);
    }
    
    public double SelectionX
    {
        get => _selectionX;
        set => this.RaiseAndSetIfChanged(ref _selectionX, value);
    }
    
    public double SelectionY
    {
        get => _selectionY;
        set => this.RaiseAndSetIfChanged(ref _selectionY, value);
    }
    
    public double SelectionWidth
    {
        get => _selectionWidth;
        set => this.RaiseAndSetIfChanged(ref _selectionWidth, value);
    }
    
    public double SelectionHeight
    {
        get => _selectionHeight;
        set => this.RaiseAndSetIfChanged(ref _selectionHeight, value);
    }
    
    public string SelectionInfo
    {
        get => _selectionInfo;
        set => this.RaiseAndSetIfChanged(ref _selectionInfo, value);
    }
    
    public ReactiveCommand<Unit, Unit> SelectImageCommand { get; }
    public ReactiveCommand<Unit, Unit> StartSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> AnalyzeCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowHistoryCommand { get; }
    
    public MainWindowViewModel(MainWindow window)
    {
        _window = window;
        _palmReaderService = new PalmReaderService();
        _predictionService = new PredictionService();
        _historyService = new HistoryService();
        
        SelectImageCommand = ReactiveCommand.CreateFromTask(SelectImage);
        StartSelectionCommand = ReactiveCommand.Create(StartSelection);
        ResetSelectionCommand = ReactiveCommand.Create(ResetSelection);
        AnalyzeCommand = ReactiveCommand.CreateFromTask(AnalyzePalm);
        ShowHistoryCommand = ReactiveCommand.CreateFromTask(ShowHistory);
    }
    
    private async Task SelectImage()
    {
        var topLevel = TopLevel.GetTopLevel(_window);
        if (topLevel == null) return;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите изображение ладони",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp" } }
            }
        });
        
        if (files.Count > 0)
        {
            var file = files[0];
            _originalImagePath = file.Path.LocalPath;
            
            try
            {
                CurrentImage = new Bitmap(_originalImagePath);
                HasImage = true;
                HasSelection = false;
                _selectionRect = null;
                StatusMessage = "✅ Изображение загружено. Выделите область ладони для точного анализа.";
                CurrentPrediction = "✨ Выделите область ладони и нажмите 'Начать гадание' ✨";
                SelectionInfo = "Область не выделена";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Ошибка загрузки: {ex.Message}";
            }
        }
    }
    
    private void StartSelection()
    {
        IsSelecting = true;
        StatusMessage = "🎯 Выделите область ладони мышкой. ESC - отмена, Enter - применить";
    }
    
    public void StartSelection(double x, double y)
    {
        _startX = Math.Clamp(x, 0, 450);
        _startY = Math.Clamp(y, 0, 450);
        SelectionX = _startX;
        SelectionY = _startY;
        SelectionWidth = 0;
        SelectionHeight = 0;
        IsDragging = true;
    }
    
    public void UpdateSelection(double x, double y)
    {
        var currentX = Math.Clamp(x, 0, 450);
        var currentY = Math.Clamp(y, 0, 450);
        
        SelectionX = Math.Min(_startX, currentX);
        SelectionY = Math.Min(_startY, currentY);
        SelectionWidth = Math.Abs(currentX - _startX);
        SelectionHeight = Math.Abs(currentY - _startY);
    }
    
    public void EndSelection()
    {
        IsDragging = false;
        
        if (SelectionWidth > 10 && SelectionHeight > 10)
        {
            _selectionRect = new SelectionRectangle(
                (int)SelectionX,
                (int)SelectionY,
                (int)SelectionWidth,
                (int)SelectionHeight
            );
            HasSelection = true;
            SelectionInfo = $"Выделена область: {_selectionRect.Value.Width}x{_selectionRect.Value.Height} пикселей";
            StatusMessage = "✅ Область выделена. Нажмите 'Начать гадание' для анализа.";
        }
        else
        {
            SelectionInfo = "Область не выделена";
        }
        
        IsSelecting = false;
    }
    
    public void CancelSelection()
    {
        IsSelecting = false;
        IsDragging = false;
        SelectionX = SelectionY = SelectionWidth = SelectionHeight = 0;
        StatusMessage = "❌ Выделение отменено.";
    }
    
    public void ApplySelection()
    {
        EndSelection();
    }
    
    private void ResetSelection()
    {
        CurrentImage = null;
        _originalImagePath = null;
        _processedImagePath = null;
        HasImage = false;
        HasSelection = false;
        IsSelecting = false;
        IsDragging = false;
        _selectionRect = null;
        SelectionX = SelectionY = SelectionWidth = SelectionHeight = 0;
        CurrentPrediction = "✨ Загрузите фото, выделите область ладони и нажмите 'Начать гадание' ✨";
        LifeLineAnalysis = "Ожидание анализа...";
        HeartLineAnalysis = "Ожидание анализа...";
        HeadLineAnalysis = "Ожидание анализа...";
        ComplexityNote = "Загрузите фото и выделите область ладони для точного анализа";
        SelectionInfo = "Область не выделена";
        StatusMessage = "🔄 Сброшено. Загрузите новое фото.";
    }
    
    private async Task AnalyzePalm()
    {
        if (string.IsNullOrEmpty(_originalImagePath))
        {
            StatusMessage = "⚠️ Сначала загрузите изображение!";
            return;
        }
        
        StatusMessage = "🔮 Анализ ладони... Пожалуйста, подождите.";
        CurrentPrediction = "Анализируем линии вашей руки...";
        
        await Task.Run(() =>
        {
            try
            {
                _processedImagePath = _palmReaderService.DrawLinesOnImage(
                    _originalImagePath, 
                    450, 
                    450, 
                    _selectionRect
                );
                
                var analysis = _palmReaderService.AnalyzePalm(_originalImagePath, _selectionRect);
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    CurrentImage = new Bitmap(_processedImagePath);
                    
                    LifeLineAnalysis = analysis.LifeLine;
                    HeartLineAnalysis = analysis.HeartLine;
                    HeadLineAnalysis = analysis.HeadLine;
                    CurrentPrediction = analysis.OverallPrediction;
                    
                    if (HasSelection)
                    {
                        ComplexityNote = "✨ Анализ выполнен по выделенной области. Точность повышена!";
                    }
                    else
                    {
                        ComplexityNote = "✨ Анализ выполнен по всей ладони.";
                    }
                    
                    StatusMessage = $"✨ Анализ завершён. Точность: {analysis.Confidence:P0}";
                    
                    _historyService.AddPrediction(_processedImagePath, CurrentPrediction);
                });
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = $"❌ Ошибка анализа: {ex.Message}";
                    CurrentPrediction = _predictionService.GetRandomPrediction();
                    LifeLineAnalysis = "❤️ Линия жизни глубокая и чёткая - вы обладаете сильной жизненной энергией.";
                    HeartLineAnalysis = "💖 Линия сердца выражена ярко - вы способны на глубокие чувства.";
                    HeadLineAnalysis = "🧠 Линия ума чёткая - вы принимаете взвешенные решения.";
                    ComplexityNote = "Несмотря на ошибку анализа, ваша ладонь говорит о большом потенциале.";
                });
            }
        });
    }
    
    private async Task ShowHistory()
    {
        var historyWindow = new HistoryWindow(_historyService);
        await historyWindow.ShowDialog(_window);
    }
}