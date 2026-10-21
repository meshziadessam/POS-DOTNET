using System;
using System.Collections.Generic;
using System.Linq;

namespace RestaurantPos
{
    public sealed class CartLine
    {
        public string ItemName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; } = 1;
        public List<string> Extras { get; set; } = new List<string>();
        public List<decimal> ExtrasPrices { get; set; } = new List<decimal>();
        public List<string> Options { get; set; } = new List<string>();
        public string Variant { get; set; } = "";
        public decimal LineDiscount { get; set; }
        public decimal Subtotal { get { return Math.Max(0, (UnitPrice + ExtrasPrices.Sum()) * Quantity - LineDiscount); } }
        public override string ToString() { return Quantity + " × " + ItemName + (Variant.Length > 0 ? " (" + Variant + ")" : "") + (Options.Count>0?" + "+string.Join(" + ",Options):"") + " = " + Subtotal.ToString("0.00") + " ج"; }
    }

    public sealed class Cart
    {
        public const string Delivery = "دليفري";
        public const string Takeaway = "تيك اواي";
        public string OrderType { get; set; } = Takeaway;
        public List<CartLine> Lines { get; } = new List<CartLine>();
        public decimal Discount { get; set; }
        public decimal DeliveryFee { get; set; }
        public string DineSubtype { get; set; } = "شركات";
        public string CompanyName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string CustomerAddress { get; set; } = "";
        public string CustomerNotes { get; set; } = "";
        public string DriverName { get; set; } = "";
        public string DriverPhone { get; set; } = "";
        public int? DriverId { get; set; }
        public int? TableNumber { get; set; }
        public string Notes { get; set; } = "";
        public long? EditingOrder { get; set; }
        public long? EditingShift { get; set; }
        public string SaveToken { get; set; } = Guid.NewGuid().ToString("N");
        public List<long> MergedWaiting { get; set; } = new List<long>();
        public decimal Subtotal { get { return Lines.Sum(x => x.Subtotal); } }
        // Matches CartState.total. Historical OrderRecord uses a different clamp order.
        public decimal Total { get { return Math.Max(0, Subtotal - Discount) + (OrderType == Delivery ? Math.Max(0, DeliveryFee) : 0); } }
    }
}
