using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PastPort.Domain.Enums
{
    public enum SubscriptionStatus
    {
        PendingPayment = 0,   // Created but payment not yet confirmed
        Active = 1,   // Payment confirmed, access granted
        PastDue = 2,   // Renewal payment failed
        Cancelled = 3,   // Voluntarily cancelled; may still have access until PeriodEnd
        Expired = 4,   // PeriodEnd passed and not renewed
        Trialing = 5,   // Free trial period
        Paused = 6    // Admin/user paused
    }
}
