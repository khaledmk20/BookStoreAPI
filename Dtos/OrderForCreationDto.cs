using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace BookStoreAPI.Dtos
{
    public class OrderForCreationDto
    {
        public string? OrderStatus { get; set; }
        public decimal TotalAmount { get; set; }
        public class OrderLineDto
        {
            public int BookId { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }

        }
        public OrderLineDto[]? OrderLine { get; set; }


    }
}