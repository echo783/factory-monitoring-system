using FactoryApi.Infrastructure.Persistence;
using FactoryApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FactoryApi.Controllers
{
    [ApiController]
    [Route("api/delivery")]
    public class DeliveryController : ControllerBase
    {
        private readonly DeliveryRepository _repository;

        public DeliveryController(DeliveryRepository repository)
        {
            _repository = repository;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DeliveryCreateRequest req)
        {
            try
            {
                long deliveryId = await _repository.CreateDeliveryAsync(req);

                return Ok(new
                {
                    success = true,
                    deliveryId
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetList()
        {
            var list = await _repository.GetListAsync();
            return Ok(list);
        }

    }
}