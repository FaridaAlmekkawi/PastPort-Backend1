using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PastPort.Domain.Enums
{
    public enum PaymentGateway
    {
        Stripe = 1,
        PayPal = 2,
        Paymob = 3,   // Popular in MENA/Egypt
        Manual = 99
    }

}
