
using System.ComponentModel.DataAnnotations;

namespace BookStoreAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        [EmailAddress]
        public string Email { get; set; } = "";
        [RegularExpression("^(?!0+$)(\\+\\d{1,3}[- ]?)?(?!0+$)\\d{10,15}$", ErrorMessage = "Please enter valid phone no.")]
        public string PhoneNumber { get; set; } = "";
        public string Gender { get; set; } = "";
        public string UserRole { get; set; } = "";
    }
}