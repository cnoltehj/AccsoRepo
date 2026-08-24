using System;
using System.Collections.Generic;
using System.Text;

namespace AccsoCourier.Domain.Enums
{
    public enum ProcessingOutcome
    {
        Applied,
        Duplicate,
        Stale,
        Conflict
    }
}
