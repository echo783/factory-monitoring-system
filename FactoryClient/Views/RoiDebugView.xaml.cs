using System;
using System.Windows.Controls;

namespace FactoryClient.Views
{
    public partial class RoiDebugView : UserControl
    {
        private const string RoiUrl = "https://localhost:7125/index.html";

        public RoiDebugView()
        {
            InitializeComponent();
            Loaded += RoiDebugView_Loaded;
        }

        private async void RoiDebugView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                await Browser.EnsureCoreWebView2Async();
                Browser.Source = new Uri(RoiUrl);
                TxtUrl.Text = RoiUrl;
            }
            catch (Exception ex)
            {
                TxtUrl.Text = "페이지 로드 실패: " + ex.Message;
            }
        }
    }
}