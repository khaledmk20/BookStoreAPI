
namespace BookStoreAPI.Dtos
{
    public class OrderForCreationDto
    {
        public class OrderLineDto
        {
            public int BookId { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
        }
        public OrderLineDto[]? OrderLine { get; set; }


    }
}