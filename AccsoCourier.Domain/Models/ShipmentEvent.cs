using System;
using System.Collections.Generic;
using System.Text;
using AccsoCourier.Domain.Enums;

namespace AccsoCourier.Domain.Models
{
    public sealed record ShipmentEvent(
    string EventId,
    string Partner,
    string ShipmentId,
    ShipmentStatus Status,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    string? Location);
}
