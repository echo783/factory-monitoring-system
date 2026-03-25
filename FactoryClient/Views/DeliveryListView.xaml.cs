using FactoryClient.ViewModels;
using System.Windows.Controls;

namespace FactoryClient.Views
{
    public partial class DeliveryListView : UserControl
    {
        private readonly DeliveryListViewModel _vm = new();

        public DeliveryListView()
        {
            InitializeComponent();
            DataContext = _vm;
            Loaded += DeliveryListView_Loaded;
        }

        private async void DeliveryListView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            await _vm.LoadAsync();
        }
    }
}