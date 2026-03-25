using FactoryClient.ViewModels;
using System.Windows.Controls;

namespace FactoryClient.Views
{
    public partial class DeliveryCreateView : UserControl
    {
        public DeliveryCreateView()
        {
            InitializeComponent();
            DataContext = new DeliveryCreateViewModel();
        }

    }
}