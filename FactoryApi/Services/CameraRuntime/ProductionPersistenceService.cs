using Microsoft.EntityFrameworkCore;
using FactoryApi.Models;
using FactoryApi.Infrastructure.Persistence;

namespace FactoryApi.Services.CameraRuntime
{
    public sealed class ProductionPersistenceService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ProductionPersistenceService> _logger;

        public ProductionPersistenceService(
            IServiceScopeFactory scopeFactory,
            ILogger<ProductionPersistenceService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<CameraConfig?> GetCameraConfigAsync(
            int cameraId,
            CancellationToken token = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FactoryDbContext>();

                return await db.CameraConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CameraId == cameraId && x.Enabled, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CameraConfig 조회 실패. CameraId={CameraId}", cameraId);
                return null;
            }
        }

        public async Task SaveProductionEventAsync(
            int cameraId,
            string productName,
            DateTime eventTime,
            int productionCount,
            string? snapshotPath,
            CancellationToken token = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FactoryDbContext>();

                var evt = new ProductionEvent
                {
                    CameraId = cameraId,
                    ProductName = productName,
                    EventTime = eventTime,
                    ProductionCount = productionCount,
                    SnapshotPath = snapshotPath,
                    CreatedAt = DateTime.Now
                };

                db.ProductionEvents.Add(evt);
                await db.SaveChangesAsync(token);

                _logger.LogInformation(
                    "ProductionEvent 저장 완료. CameraId={CameraId}, ProductName={ProductName}, Count={Count}, EventId={EventId}",
                    cameraId, productName, productionCount, evt.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProductionEvent 저장 실패. CameraId={CameraId}", cameraId);
            }
        }
    }
}