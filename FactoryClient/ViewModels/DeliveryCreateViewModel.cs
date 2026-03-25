using FactoryClient.Models;
using FactoryClient.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace FactoryClient.ViewModels
{
    public class DeliveryCreateViewModel : ViewModelBase
    {
        private readonly DeliveryApiService _apiService = new();
        public ObservableCollection<string> ProductList { get; } = new();

    private string _customerName = "";
        public string CustomerName
        {
            get => _customerName;
            set
            {
                _customerName = value;
                OnPropertyChanged();
            }
        }

        private string? _phoneNumber;
        public string? PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                _phoneNumber = value;
                OnPropertyChanged();
            }
        }

        private string? _address;
        public string? Address
        {
            get => _address;
            set
            {
                _address = value;
                OnPropertyChanged();
            }
        }

        private DateTime _deliveryDate = DateTime.Now;
        public DateTime DeliveryDate
        {
            get => _deliveryDate;
            set
            {
                _deliveryDate = value;
                OnPropertyChanged();
            }
        }

        private string _productName = "";
        public string ProductName
        {
            get => _productName;
            set
            {
                if (_productName == value)
                {
                    _ = LoadStockAsync();
                    return;
                }

                _productName = value ?? "";
                OnPropertyChanged();

                _ = LoadStockAsync();
            }
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged();
            }
        }

        private string? _memo;
        public string? Memo
        {
            get => _memo;
            set
            {
                _memo = value;
                OnPropertyChanged();
            }
        }

        private string _statusMessage = "입력 대기";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                _isSaving = value;
                OnPropertyChanged();
            }
        }

        private int _currentStock;
        public int CurrentStock
        {
            get => _currentStock;
            set
            {
                _currentStock = value;
                OnPropertyChanged();
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }

        public DeliveryCreateViewModel()
        {
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => !IsSaving);
            ResetCommand = new RelayCommand(_ => ResetForm());
            _ = LoadProductsAsync();
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                MessageBox.Show("주문자명을 입력해주세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(ProductName))
            {
                MessageBox.Show("품목명을 입력해주세요.");
                return;
            }

            if (Quantity <= 0)
            {
                MessageBox.Show("수량은 1 이상이어야 합니다.");
                return;
            }

            if (Quantity > CurrentStock)
            {
                MessageBox.Show($"현재 재고({CurrentStock})보다 많이 출고할 수 없습니다.");
                return;
            }

            try
            {
                IsSaving = true;
                StatusMessage = "저장 중...";

                var request = new DeliveryCreateRequest
                {
                    CustomerName = CustomerName,
                    PhoneNumber = PhoneNumber,
                    Address = Address,
                    DeliveryDate = DeliveryDate,
                    ProductName = ProductName,
                    Quantity = Quantity,
                    Memo = Memo
                };

                var result = await _apiService.CreateDeliveryAsync(request);

                if (result.Success)
                {
                    StatusMessage = "저장 완료";
                    MessageBox.Show("납품이 등록되었습니다.");

                    // ✅ 품목은 유지
                    CustomerName = "";
                    PhoneNumber = "";
                    Address = "";
                    DeliveryDate = DateTime.Now;
                    Quantity = 1;
                    Memo = "";

                    // ✅ 같은 품목의 최신 재고 다시 조회
                    await LoadStockAsync();
                }
                else
                {
                    StatusMessage = "저장 실패";
                    MessageBox.Show(result.Message);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "저장 실패";
                MessageBox.Show(ex.Message);
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void ResetForm()
        {
            CustomerName = "";
            PhoneNumber = "";
            Address = "";
            DeliveryDate = DateTime.Now;
            ProductName = "";
            Quantity = 1;
            Memo = "";
            CurrentStock = 0;
            StatusMessage = "입력 초기화";
        }

        public async Task LoadProductsAsync()
        {
            try
            {
                var list = await _apiService.GetProductListAsync();

                ProductList.Clear();

                foreach (var item in list)
                {
                    ProductList.Add(item);
                }
            }
            catch
            {
                // 조용히 실패 (나중에 로그만 남겨도 됨)
            }
        }

        private async Task LoadStockAsync()
        {
            try
            {
                CurrentStock = 0;

                if (string.IsNullOrWhiteSpace(ProductName))
                    return;

                var stock = await _apiService.GetProductStockAsync(ProductName);
                CurrentStock = stock?.RemainQuantity ?? 0;
            }
            catch
            {
                CurrentStock = 0;
            }
        }

    }
}