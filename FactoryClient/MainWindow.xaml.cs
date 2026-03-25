using FactoryClient.Views;
using System.Windows;

namespace FactoryClient
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ShowDashboard();
        }

        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            ShowDashboard();
        }

        private void BtnCamera_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = null;
            TxtPageTitle.Text = "카메라 관리";
            TxtPageDesc.Text = "카메라 등록, 수정, 삭제를 관리합니다.";
        }

        private void BtnAnalyzer_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = null;
            TxtPageTitle.Text = "분석 설정";
            TxtPageDesc.Text = "ROI, 감지 옵션, 분석 설정을 관리합니다.";
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new HistoryView();
            TxtPageTitle.Text = "생산 조회";
            TxtPageDesc.Text = "생산 이벤트 이력을 조회합니다.";
        }

        private void ShowDashboard()
        {
            MainContent.Content = new DashboardView();
            TxtPageTitle.Text = "대시보드";
            TxtPageDesc.Text = "실시간 카메라 상태와 생산 카운트를 확인합니다.";
        }

        private void BtnRoiDebug_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new RoiDebugView();
            TxtPageTitle.Text = "카메라 ROI 디버그";
            TxtPageDesc.Text = "ROI 디버그 웹 화면을 앱 내부에서 표시합니다.";
        }

        private void BtnDeliveryList_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new DeliveryListView();
            TxtPageTitle.Text = "납품 조회";
            TxtPageDesc.Text = "등록된 납품 내역과 출고 정보를 조회합니다.";
        }
        private void BtnDeliveryCreate_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new DeliveryCreateView();
            TxtPageTitle.Text = "납품 등록";
            TxtPageDesc.Text = "주문자 정보와 품목/수량을 입력하여 납품을 등록합니다.";
        }
    }
}