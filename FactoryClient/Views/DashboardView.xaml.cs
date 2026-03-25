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

            Start();
        }

        private async void Start()
        {
            while (true)
            {
                await _vm.LoadAsync(1);
                await Task.Delay(500);
            }
        }
    }
}