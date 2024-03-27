
namespace BookStoreAPI.Models
{
    public class Cart
    {
        public int BookId { get; set; }
        public string? BookTitle { get; set; }
        public decimal BookPrice { get; set; }
        public string? BookImage { get; set; }
        public int Quantity { get; set; }
        public string? BookDescription { get; set; }
    }
}