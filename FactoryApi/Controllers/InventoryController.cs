using Dapper;
using Microsoft.AspNetCore.Mvc;
using FactoryApi.Infrastructure.Persistence;

namespace FactoryApi.Controllers
{
    [ApiController]
    [Route("api/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly SqlConnectionFactory _factory;

        public InventoryController(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            using var conn = _factory.CreateConnection();

            var sql = @"
SELECT DISTINCT ProductName
FROM dbo.Inventory
ORDER BY ProductName;
";

            var list = await conn.QueryAsync<string>(sql);

            return Ok(list);
        }

        [HttpGet("stock")]
        public async Task<IActionResult> GetStock([FromQuery] string productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
                return BadRequest("productName is required.");

            using var conn = _factory.CreateConnection();

            var sql = @"
SELECT ISNULL(SUM(RemainQuantity), 0)
FROM dbo.Inventory
WHERE ProductName = @ProductName;
";

            var stock = await conn.ExecuteScalarAsync<int>(sql, new { ProductName = productName });

            return Ok(new
            {
                productName,
                remainQuantity = stock
            });
        }

    }
}