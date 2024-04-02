
namespace BookStoreAPI.Models
{
    public class BookReview
    {
        public string? Comment { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public decimal RatingValue { get; set; }
        public string? UserImageUrl { get; set; }

        public DateTime ReviewDate { get; set; }
    }
}