
namespace BookStoreAPI.Dtos
{
    public class UserForResetPasswordDto
    {
        public string? Password { get; set; }
        public string? PasswordConfirm { get; set; }
        public string? Token { get; set; }
    }
}