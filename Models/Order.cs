using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookStoreAPI.Models
{
    public class Order
    {
        public int BookId { get; set; }
        public int OrderId { get; set; }
        public string? BookTitle { get; set; }
        public string? BookImage { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string? OrderStatus { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }
    }
}