using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookStoreAPI.Models
{
    public class Address
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string AddressLine1 { get; set; } = "";
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = "";
        public string? PostalCode { get; set; }
        public string Country { get; set; } = "";

    }
}