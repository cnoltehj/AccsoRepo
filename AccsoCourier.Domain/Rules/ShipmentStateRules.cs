using AccsoCourier.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AccsoCourier.Domain.Rules
{
    public static class ShipmentStateRules
    {
        private static readonly IReadOnlyDictionary<ShipmentStatus, ShipmentStatus[]> ValidTransitions =
        new Dictionary<ShipmentStatus, ShipmentStatus[]>
        {
            [ShipmentStatus.LabelCreated] = [ShipmentStatus.HandedToCarrier, ShipmentStatus.DeliveryException],
            [ShipmentStatus.HandedToCarrier] = [ShipmentStatus.InTransit, ShipmentStatus.DeliveryException],
            [ShipmentStatus.InTransit] = [ShipmentStatus.OutForDelivery, ShipmentStatus.DeliveryException, ShipmentStatus.Returned],
            [ShipmentStatus.OutForDelivery] = [ShipmentStatus.Delivered, ShipmentStatus.DeliveryException, ShipmentStatus.Returned],
            [ShipmentStatus.Delivered] = [],
            [ShipmentStatus.DeliveryException] = [ShipmentStatus.InTransit, ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered, ShipmentStatus.Returned],
            [ShipmentStatus.Returned] = []
        };

        public static bool IsValidTransition(ShipmentStatus current, ShipmentStatus incoming) =>
            ValidTransitions.TryGetValue(current, out var allowed) && allowed.Contains(incoming);
    }
}
