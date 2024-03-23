using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BookStoreAPI.Data;
using BookStoreAPI.Dtos;
using Dapper;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace BookStoreAPI.Helpers
{
    public class AuthHelper : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly DataContextDapper _dapper;
        public AuthHelper(IConfiguration config)
        {
            _dapper = new DataContextDapper(config);
            _config = config;
        }
        public byte[] GetPasswordHash(string password, byte[] passwordSalt)
        {
            string passwordSaltPlusString = _config.GetSection("AppSettings:PasswordKey")
                   .Value + Convert.ToBase64String(passwordSalt);

            return KeyDerivation.Pbkdf2(
                    password: password,
                    salt: Encoding.ASCII.GetBytes(passwordSaltPlusString),
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 100000,
                   numBytesRequested: 256 / 8
            );
        }

        public bool SetPassword(UserForLoginDto userForSetPassword)
        {
            byte[] passwordSalt = new byte[128 / 8];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetNonZeroBytes(passwordSalt);
            }


            byte[] passwordHash = GetPasswordHash(userForSetPassword.Password, passwordSalt);

            string sqlAddAuth = @"
                        EXEC UserSchema.spRegistration_Upsert
                         @Email = @EmailParam,
                         @passwordHash = @passwordHashParam,
                         @passwordSalt = @passwordSaltParam";

            DynamicParameters sqlParameters = new DynamicParameters();

            sqlParameters.Add("@EmailParam", userForSetPassword.Email, DbType.String);
            sqlParameters.Add("@passwordHashParam", passwordHash, DbType.Binary);
            sqlParameters.Add("@passwordSaltParam", passwordSalt, DbType.Binary);

            return _dapper.ExecuteSqlWithParameters(sqlAddAuth, sqlParameters);

        }

        public bool IsValidEmail(string email)
        {
            // Simple email validation regex
            string emailRegex = @"^[^\s@]+@[^\s@]+\.[^\s@]+$";
            return Regex.IsMatch(email, emailRegex);
        }

        public string CreateToken(int userId, string userRole)
        {
            Claim[] claims = new Claim[] {
                new Claim("userId",userId.ToString()),
                new Claim("userRole",userRole)
            };
            string? tokenKeyString = _config.GetSection("Jwt:Key").Value;
            SymmetricSecurityKey tokenKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKeyString is not null ? tokenKeyString : ""));

            SigningCredentials credentials = new SigningCredentials(tokenKey, SecurityAlgorithms.HmacSha512Signature);

            SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor()
            {
                Subject = new ClaimsIdentity(claims),
                SigningCredentials = credentials,
                Expires = DateTime.Now.AddDays(1)
            };

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

            SecurityToken token = tokenHandler.CreateToken(descriptor);

            return tokenHandler.WriteToken(token);

        }
        public bool IsAdmin(ClaimsPrincipal user)
        {
            var userRoleClaim = user.FindFirstValue("userRole");
            if (userRoleClaim?.ToLower() == "admin")
                return true;
            return false;
        }


    }
}