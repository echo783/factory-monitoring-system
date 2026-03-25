using FactoryClient.ViewModels;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FactoryClient.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly DashboardViewModel _vm = new DashboardViewModel();

        public DashboardView()
        {
            InitializeComponent();
            DataContext = _vm;

            Loaded += DashboardView_Loaded;
        }

        private async void DashboardView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            await _vm.InitializeAsync(1);

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await _vm.LoadAsync(1);
                    await Task.Delay(500);
                }
            });
        }
    }
}