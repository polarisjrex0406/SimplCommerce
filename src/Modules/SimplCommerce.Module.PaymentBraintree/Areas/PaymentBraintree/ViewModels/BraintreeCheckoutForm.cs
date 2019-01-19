﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SimplCommerce.Module.PaymentBraintree.Areas.PaymentBraintree.ViewModels
{
    public class BraintreeCheckoutForm
    {
        public string ClientToken { get; set; }

        public int Amount { get; set; }

        public string ISOCurrencyCode { get; set; }
    }
}
