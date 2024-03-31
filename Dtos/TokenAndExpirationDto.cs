using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookStoreAPI.Dtos
{
    public class TokenAndExpirationDto
    {
        public string? ResetPasswordToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}