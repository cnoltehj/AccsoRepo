using System.ComponentModel.DataAnnotations;

namespace AccsoCourier.API.Contracts
{
    public class CourierEventRequest
    {
        [Required] public string EventId { get; init; } = "";
        [Required] public string Partner { get; init; } = "";
        [Required] public string ShipmentId { get; init; } = "";
        [Required] public string Status { get; init; } = "";
        public DateTimeOffset OccurredAt { get; init; }
        public DateTimeOffset ReceivedAt { get; init; }
        public string? Location { get; init; }
    }
}
