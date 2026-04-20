using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PastPort.Domain.Enums
{
    public enum TransactionStatus
    {
        Pending = 0,   // Initiated, waiting for gateway response
        Success = 1,   // Gateway confirmed payment
        Failed = 2,   // Gateway rejected payment
        Refunded = 3,   // Full refund issued
        Disputed = 4,   // Chargeback / dispute opened
        Cancelled = 5    // Cancelled before gateway processed
    }
}
