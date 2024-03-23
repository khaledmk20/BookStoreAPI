namespace BookStoreAPI.Models
{
    public class Book
    {
        public int? Id { get; set; }
        public string? BookTitle { get; set; }
        public string? AuthorName { get; set; }
        public string? CategoryName { get; set; }
        public decimal BookPrice { get; set; }
        public string? BookImage { get; set; }
        public string? BookDescription { get; set; }
        public int? QuantityInStock { get; set; }
        public int? PublicationYear { get; set; }
    }
}