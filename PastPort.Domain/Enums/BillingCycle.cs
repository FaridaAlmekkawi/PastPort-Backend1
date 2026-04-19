using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PastPort.Domain.Enums
{
    public enum BillingCycle
    {
        Monthly = 1,
        Yearly = 2,
        Weekly = 3,
        OneTime = 4    // Lifetime / one-time purchase
    }
}
