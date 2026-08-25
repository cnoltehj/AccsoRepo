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
            logger.LogInformation("Request received to retrieve current state for shipment {ShipmentId}.",shipmentId);
            var state = await repository.GetCurrentStateAsync(shipmentId);
            if (state is null)
            {
                logger.LogError("NotFound - state for shipment {ShipmentId}: {@State}", shipmentId, state);\
                return NotFound();
            }
            logger.LogInformation( "Successfully retrieved current state for shipment {ShipmentId}. Status={Status}, EventId={EventId}.",
                shipmentId, state.Status, state.EventId);
            return state is null ? NotFound() : Ok(state);
        }


        [HttpGet("{shipmentId}/events_get_history")]
        public async Task<IActionResult> GetHistory(string shipmentId) 
        {
            logger.LogInformation("Request received to retrieve event history for shipment {ShipmentId}.",shipmentId);
            var history = await repository.GetHistoryAsync(shipmentId);
            logger.LogInformation(
                "Retrieved {EventCount} event(s) for shipment {ShipmentId}.",history.Count,shipmentId);
            return Ok(history);
        }
    }
}
