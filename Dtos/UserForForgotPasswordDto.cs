using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BookStoreAPI.Dtos
{
    public class UserForForgotPasswordDto
    {
        public string? Email { get; set; }
    }
}