using System;
using System.Collections.Generic;
using System.Text;
using AccsoCourier.Domain.Enums;

namespace AccsoCourier.Domain.Models
{
    public sealed record ProcessingResult(
    ProcessingOutcome Outcome,
    string ShipmentId,
    string EventId,
    string? Reason = null);
}
