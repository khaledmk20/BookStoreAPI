
namespace BookStoreAPI.Models
{
    public class Cart
    {
        public int BookId { get; set; }
        public string? BookTitle { get; set; }
        public string? BookPrice { get; set; }
        public string? BookImage { get; set; }
        public string? Quantity { get; set; }
    }
}