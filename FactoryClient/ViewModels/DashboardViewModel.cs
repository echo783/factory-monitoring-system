using FactoryClient.Models;
using FactoryClient.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FactoryClient.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly CameraApiService _apiService = new CameraApiService();

        private string _pageTitle = "대시보드";
        private string _pageDescription = "실시간 카메라 상태와 생산 카운트를 확인합니다.";
        private string _productionCount = "0";
        private string _rotationText = "대기";
        private string _labelText = "대기";
        private string _debugStateText = "상태 대기중";
        private BitmapImage? _cameraImage;

        public string PageTitle
        {
            get => _pageTitle;
            set
            {
                _pageTitle = value;
                OnPropertyChanged();
            }
        }

        public string PageDescription
        {
            get => _pageDescription;
            set
            {
                _pageDescription = value;
                OnPropertyChanged();
            }
        }

        public string ProductionCount
        {
            get => _productionCount;
            set
            {
                _productionCount = value;
                OnPropertyChanged();
            }
        }

        public string RotationText
        {
            get => _rotationText;
            set
            {
                _rotationText = value;
                OnPropertyChanged();
            }
        }

        public string LabelText
        {
            get => _labelText;
            set
            {
                _labelText = value;
                OnPropertyChanged();
            }
        }

        public string DebugStateText
        {
            get => _debugStateText;
            set
            {
                _debugStateText = value;
                OnPropertyChanged();
            }
        }

        public BitmapImage? CameraImage
        {
            get => _cameraImage;
            set
            {
                _cameraImage = value;
                OnPropertyChanged();
            }
        }

        public async Task LoadAsync(int cameraId = 1)
        {
            try
            {
                DebugStateDto? state = await _apiService.GetDebugStateAsync(cameraId);

                if (state != null)
                {
                    ProductionCount = state.ProductionCount.ToString();
                    RotationText = state.RotationActive ? "감지중" : "대기";
                    LabelText = state.LabelInZone ? "감지중" : "대기";

                    DebugStateText =
                        $"RotationActive           : {state.RotationActive}\n" +
                        $"LabelInZone              : {state.LabelInZone}\n" +
                        $"CountedInCurrentRotation : {state.CountedInCurrentRotation}\n\n" +
                        $"LastStarted              : {state.LastStarted}\n" +
                        $"LastEnded                : {state.LastEnded}\n" +
                        $"LastLabelEnter           : {state.LastLabelEnter}\n\n" +
                        $"LastDetectorFound        : {state.LastDetectorFound}\n" +
                        $"LastDetectorConfidence   : {state.LastDetectorConfidence}\n\n" +
                        $"LabelDetectedStreak      : {state.LabelDetectedStreak}\n" +
                        $"LabelOffStreak           : {state.LabelOffStreak}\n\n" +
                        $"LastRotationChangeValue  : {state.LastRotationChangeValue}\n" +
                        $"LastMotionRatio          : {state.LastMotionRatio}\n" +
                        $"LastLabelChangeValue     : {state.LastLabelChangeValue}\n\n" +
                        $"ProductionCount          : {state.ProductionCount}\n" +
                        $"LastUpdated              : {state.LastUpdatedAt}";
                }

                CameraImage = await _apiService.GetCameraImageAsync(cameraId);
            }
            catch (Exception ex)
            {
                DebugStateText = "API 오류\n" + ex.Message;
            }
        }
    }
}