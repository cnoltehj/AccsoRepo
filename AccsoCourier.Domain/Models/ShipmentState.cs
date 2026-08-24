using System;
using System.Collections.Generic;
using System.Text;
using AccsoCourier.Domain.Enums;

namespace AccsoCourier.Domain.Models
{
    public sealed record ShipmentState(
    string ShipmentId,
    ShipmentStatus Status,
    string EventId,
    DateTimeOffset OccurredAt,
    string? Location,
    byte[]? RowVersion = null);
}
