using System.Data;
using Dapper;
using FactoryApi.Data;
using FactoryApi.Models;

namespace FactoryApi.Repositories
{
    public class DeliveryRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;

        public DeliveryRepository(SqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<long> CreateDeliveryAsync(DeliveryCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.CustomerName))
                throw new Exception("주문자명은 필수입니다.");

            if (string.IsNullOrWhiteSpace(req.ProductName))
                throw new Exception("품목명은 필수입니다.");

            if (req.Quantity <= 0)
                throw new Exception("수량은 1 이상이어야 합니다.");

            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            using var tx = conn.BeginTransaction();

            try
            {
                // 1. Delivery 저장
                var insertDeliverySql = @"
INSERT INTO dbo.Delivery
(
    CustomerName,
    PhoneNumber,
    Address,
    DeliveryDate,
    Memo,
    CreatedAt
)
VALUES
(
    @CustomerName,
    @PhoneNumber,
    @Address,
    @DeliveryDate,
    @Memo,
    GETDATE()
);

SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
";

                long deliveryId = await conn.ExecuteScalarAsync<long>(
                    insertDeliverySql,
                    new
                    {
                        req.CustomerName,
                        req.PhoneNumber,
                        req.Address,
                        req.DeliveryDate,
                        req.Memo
                    },
                    tx);

                // 2. DeliveryItem 저장
                var insertItemSql = @"
INSERT INTO dbo.DeliveryItem
(
    DeliveryId,
    ProductName,
    Quantity,
    CreatedAt
)
VALUES
(
    @DeliveryId,
    @ProductName,
    @Quantity,
    GETDATE()
);

SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
";

                long deliveryItemId = await conn.ExecuteScalarAsync<long>(
                    insertItemSql,
                    new
                    {
                        DeliveryId = deliveryId,
                        req.ProductName,
                        req.Quantity
                    },
                    tx);

                // 3. FIFO 재고 조회
                var inventorySql = @"
SELECT
    InventoryId,
    ProductName,
    InDate,
    InQuantity,
    RemainQuantity,
    CreatedAt
FROM dbo.Inventory
WHERE ProductName = @ProductName
  AND RemainQuantity > 0
ORDER BY InDate ASC, InventoryId ASC;
";

                var inventories = (await conn.QueryAsync<InventoryRow>(
                    inventorySql,
                    new { req.ProductName },
                    tx)).ToList();

                int remain = req.Quantity;

                foreach (var inv in inventories)
                {
                    if (remain <= 0)
                        break;

                    int deduct = Math.Min(inv.RemainQuantity, remain);

                    // 4. 재고 차감
                    var updateInventorySql = @"
UPDATE dbo.Inventory
SET RemainQuantity = RemainQuantity - @Deduct
WHERE InventoryId = @InventoryId;
";

                    await conn.ExecuteAsync(
                        updateInventorySql,
                        new
                        {
                            Deduct = deduct,
                            inv.InventoryId
                        },
                        tx);

                    // 5. 차감 이력 저장
                    var insertHistorySql = @"
INSERT INTO dbo.StockOutHistory
(
    DeliveryId,
    DeliveryItemId,
    InventoryId,
    ProductName,
    OutQuantity,
    OutDate
)
VALUES
(
    @DeliveryId,
    @DeliveryItemId,
    @InventoryId,
    @ProductName,
    @OutQuantity,
    GETDATE()
);
";

                    await conn.ExecuteAsync(
                        insertHistorySql,
                        new
                        {
                            DeliveryId = deliveryId,
                            DeliveryItemId = deliveryItemId,
                            InventoryId = inv.InventoryId,
                            ProductName = req.ProductName,
                            OutQuantity = deduct
                        },
                        tx);

                    remain -= deduct;
                }

                if (remain > 0)
                    throw new Exception("재고가 부족합니다.");

                tx.Commit();
                return deliveryId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private sealed class InventoryRow
        {
            public long InventoryId { get; set; }
            public string ProductName { get; set; } = "";
            public DateTime InDate { get; set; }
            public int InQuantity { get; set; }
            public int RemainQuantity { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public async Task<List<DeliveryListDto>> GetListAsync()
        {
            using var conn = _connectionFactory.CreateConnection();

            var sql = @"
SELECT
    d.DeliveryId,
    d.DeliveryDate,
    d.CustomerName,
    d.PhoneNumber,
    d.Address,
    di.ProductName,
    di.Quantity,
    d.CreatedAt
FROM dbo.Delivery d
INNER JOIN dbo.DeliveryItem di
    ON d.DeliveryId = di.DeliveryId
ORDER BY d.DeliveryId DESC;
";

            var list = await conn.QueryAsync<DeliveryListDto>(sql);

            return list.ToList();
        }

    }

}