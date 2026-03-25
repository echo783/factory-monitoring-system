using FactoryClient.Models;
using FactoryClient.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace FactoryClient.ViewModels
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly CameraApiService _apiService = new();

        public ObservableCollection<ProductionEventDto> Events { get; } = new();

        private ProductionEventDto? _selectedEvent;
        public ProductionEventDto? SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                _selectedEvent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedImageUrl));
            }
        }

        private DateTime _fromDate = DateTime.Today;
        public DateTime FromDate
        {
            get => _fromDate;
            set
            {
                _fromDate = value;
                OnPropertyChanged();
            }
        }

        private DateTime _toDate = DateTime.Today;
        public DateTime ToDate
        {
            get => _toDate;
            set
            {
                _toDate = value;
                OnPropertyChanged();
            }
        }

        private string _statusMessage = "조회 대기";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string SelectedImageUrl
        {
            get
            {
                if (SelectedEvent == null || string.IsNullOrWhiteSpace(SelectedEvent.ImagePath))
                    return "";

                var fileName = System.IO.Path.GetFileName(SelectedEvent.ImagePath);
                return $"https://localhost:7125/captures/cam1/{fileName}";
            }
        }

        public ICommand SearchCommand { get; }

        public HistoryViewModel()
        {
            SearchCommand = new RelayCommand(async _ => await LoadAsync());
        }

        public async Task LoadAsync()
        {
            try
            {
                StatusMessage = "조회 중...";

                var from = FromDate.Date;
                var to = ToDate.Date.AddDays(1).AddSeconds(-1);

                var items = await _apiService.GetProductionEventsAsync(null, from, to);

                Events.Clear();
                foreach (var item in items.OrderByDescending(x => x.EventTime))
                {
                    Events.Add(item);
                }

                StatusMessage = $"조회 완료: {Events.Count}건";
            }
            catch (Exception ex)
            {
                StatusMessage = "조회 실패: " + ex.Message;
            }
        }
    }
}