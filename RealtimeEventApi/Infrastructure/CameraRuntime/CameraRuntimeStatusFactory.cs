using RealtimeEventApi.Contracts.Responses.Camera;

namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    public sealed class CameraRuntimeStatusFactory
    {
        public CameraRunStatusResponse Create(
            int cameraId,
            string cameraName,
            bool enabled,
            bool sessionExists,
            CameraSessionSnapshot? state)
        {
            var (status, message) = GetRuntimeStatus(enabled, sessionExists, state);

            return new CameraRunStatusResponse
            {
                CameraId = cameraId,
                CameraName = cameraName,
                Enabled = enabled,
                Status = status,
                Message = message,
                ChangedAt = DateTime.Now,
                LastSuccessfulReadAt = ToNullableTime(state?.LastSuccessfulReadAt),
                LastErrorAt = ToNullableTime(state?.LastErrorAt),
                LastErrorMessage = state?.LastErrorMessage ?? string.Empty,
                ProductionCount = state?.ProductionCount ?? 0
            };
        }

        public static string BuildSignature(CameraRunStatusResponse status)
        {
            return string.Join(
                "|",
                status.Status,
                status.Message,
                status.LastSuccessfulReadAt?.Ticks.ToString() ?? "",
                status.LastErrorAt?.Ticks.ToString() ?? "",
                status.LastErrorMessage,
                status.ProductionCount.ToString());
        }

        private static (string Status, string Message) GetRuntimeStatus(
            bool enabled,
            bool sessionExists,
            CameraSessionSnapshot? state)
        {
            if (!enabled && !sessionExists)
                return ("Stopped", "현재 중지 상태입니다.");

            if (enabled && !sessionExists)
                return ("Starting", "실행 요청 상태이지만 아직 세션이 준비되지 않았습니다.");

            if (!enabled && sessionExists)
                return ("Stopping", "중지 요청 상태이지만 세션 정리 중일 수 있습니다.");

            if (state == null || state.LastSuccessfulReadAt == DateTime.MinValue)
            {
                var message = string.IsNullOrWhiteSpace(state?.LastErrorMessage)
                    ? "세션은 시작됐지만 아직 카메라 프레임을 수신하지 못했습니다."
                    : state.LastErrorMessage;

                return ("Connecting", message);
            }

            var age = DateTime.Now - state.LastSuccessfulReadAt;
            if (age.TotalSeconds > 10)
            {
                var message = string.IsNullOrWhiteSpace(state.LastErrorMessage)
                    ? $"마지막 프레임 수신 후 {age.TotalSeconds:F0}초가 지났습니다. 스트림 연결을 확인하세요."
                    : state.LastErrorMessage;

                return ("Stale", message);
            }

            return ("Running", "현재 프레임을 정상 수신 중입니다.");
        }

        private static DateTime? ToNullableTime(DateTime? value)
        {
            if (value == null || value.Value == DateTime.MinValue)
                return null;

            return value.Value;
        }
    }
}
