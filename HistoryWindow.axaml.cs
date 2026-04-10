using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using predskoz.Services;
using predskoz.Models;

namespace predskoz;

public partial class HistoryWindow : Window
{
    public HistoryWindowViewModel ViewModel { get; }
    
    public HistoryWindow(HistoryService historyService)
    {
        InitializeComponent();
        ViewModel = new HistoryWindowViewModel(historyService);
        DataContext = ViewModel;
    }
}

public class HistoryWindowViewModel : ReactiveObject
{
    private readonly HistoryService _historyService;
    private ObservableCollection<HistoryItem> _historyItems = new();
    
    public ObservableCollection<HistoryItem> HistoryItems
    {
        get => _historyItems;
        set => this.RaiseAndSetIfChanged(ref _historyItems, value);
    }
    
    public ReactiveCommand<Unit, Unit> ClearHistoryCommand { get; }
    
    public HistoryWindowViewModel(HistoryService historyService)
    {
        _historyService = historyService;
        ClearHistoryCommand = ReactiveCommand.Create(ClearHistory);
        LoadHistory();
    }
    
    private void LoadHistory()
    {
        var history = _historyService.GetHistory();
        HistoryItems.Clear();
        
        foreach (var item in history)
        {
            HistoryItems.Add(new HistoryItem
            {
                Prediction = item.Prediction,
                Timestamp = item.Timestamp,
                ThumbnailPath = item.ThumbnailPath
            });
        }
    }
    
    private void ClearHistory()
    {
        _historyService.ClearHistory();
        HistoryItems.Clear();
    }
}

public class HistoryItem
{
    public string Prediction { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ThumbnailPath { get; set; } = string.Empty;
    public string FormattedDate => Timestamp.ToString("dd.MM.yyyy HH:mm");
}