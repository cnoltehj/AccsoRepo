using Microsoft.AspNetCore.Mvc;
using AccsoCourier.Domain.Interfaces;


namespace AccsoCourier.API.Controllers
{
    [ApiController]
    [Route("api/shipments")]
    public sealed class ShipmentsController(IShipmentRepository repository, ILogger<ShipmentsController> logger) : ControllerBase
    {
        [HttpGet("{shipmentId}/get_current_state")]
        public async Task<IActionResult> GetState(string shipmentId)
        {
            var state = await repository.GetCurrentStateAsync(shipmentId);
            logger.LogInformation("Retrieved state for shipment {ShipmentId}: {@State}", shipmentId, state);
            if (state is null)
            {
                logger.LogError("NotFound - state for shipment {ShipmentId}: {@State}", shipmentId, state);
            }
            return state is null ? NotFound() : Ok(state);
        }


        [HttpGet("{shipmentId}/events_get_history")]
        public async Task<IActionResult> GetHistory(string shipmentId) =>
            Ok(await repository.GetHistoryAsync(shipmentId)); 
        
    }
}
