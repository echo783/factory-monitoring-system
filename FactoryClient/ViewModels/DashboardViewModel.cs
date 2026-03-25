using FactoryClient.Models;
using FactoryClient.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FactoryClient.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly CameraApiService _apiService = new CameraApiService();
        private readonly CameraHubService _hubService = new CameraHubService();

        private string _pageTitle = "대시보드";
        private string _pageDescription = "실시간 카메라 상태와 생산 카운트를 확인합니다.";
        private string _productionCount = "0";
        private string _rotationText = "대기";
        private string _labelText = "대기";
        private string _debugStateText = "상태 대기중";
        private BitmapImage? _cameraImage;

        private string _cameraRunStatus = "확인중";
        private string _cameraRunMessage = "카메라 상태를 불러오는 중입니다.";

        public RelayCommand StartCameraCommand { get; }
        public RelayCommand StopCameraCommand { get; }

        public DashboardViewModel()
        {
            StartCameraCommand = new RelayCommand(async _ => await StartCameraAsync());
            StopCameraCommand = new RelayCommand(async _ => await StopCameraAsync());

            _hubService.CameraStatusChanged += payload =>
            {
                if (payload.CameraId != 1)
                    return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CameraRunStatus = payload.Status;
                    CameraRunMessage = payload.Message;
                });
            };
        }

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

        public string CameraRunStatus
        {
            get => _cameraRunStatus;
            set
            {
                _cameraRunStatus = value;
                OnPropertyChanged();
            }
        }

        public string CameraRunMessage
        {
            get => _cameraRunMessage;
            set
            {
                _cameraRunMessage = value;
                OnPropertyChanged();
            }
        }

        public async Task InitializeAsync(int cameraId = 1)
        {
            try
            {
                await _hubService.StartAsync();

                var runStatus = await _apiService.GetCameraRunStatusAsync(cameraId);
                if (runStatus != null)
                {
                    CameraRunStatus = runStatus.Status;
                    CameraRunMessage = runStatus.Message;
                }
            }
            catch (Exception ex)
            {
                CameraRunStatus = "Error";
                CameraRunMessage = "SignalR 연결 실패: " + ex.Message;
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

        private async Task StartCameraAsync()
        {
            var ok = await _apiService.StartCameraAsync(1);
            if (!ok)
            {
                CameraRunStatus = "Error";
                CameraRunMessage = "카메라 시작 요청 실패";
            }
        }

        private async Task StopCameraAsync()
        {
            var ok = await _apiService.StopCameraAsync(1);
            if (!ok)
            {
                CameraRunStatus = "Error";
                CameraRunMessage = "카메라 중지 요청 실패";
            }
        }
    }
}