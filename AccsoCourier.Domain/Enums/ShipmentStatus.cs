using System;
using System.Collections.Generic;
using System.Text;

namespace AccsoCourier.Domain.Enums
{
    public enum ShipmentStatus
    {
        LabelCreated,
        HandedToCarrier,
        InTransit,
        OutForDelivery,
        Delivered,
        DeliveryException,
        Returned
    }
}
