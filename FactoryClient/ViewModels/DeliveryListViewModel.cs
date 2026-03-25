using FactoryClient.Models;
using FactoryClient.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace FactoryClient.ViewModels
{
    public class DeliveryListViewModel : ViewModelBase
    {
        private readonly DeliveryApiService _apiService = new();

        public ObservableCollection<DeliveryListDto> Items { get; } = new();

        private DeliveryListDto? _selectedItem;
        public DeliveryListDto? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
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

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshCommand { get; }

        public DeliveryListViewModel()
        {
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        }

        public async Task LoadAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "조회 중...";

                var list = await _apiService.GetDeliveryListAsync();

                Items.Clear();
                foreach (var item in list)
                {
                    Items.Add(item);
                }

                StatusMessage = $"조회 완료: {Items.Count}건";
            }
            catch (Exception ex)
            {
                StatusMessage = "조회 실패: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}