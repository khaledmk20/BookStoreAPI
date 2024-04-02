using System.Data;
using BookStoreAPI.Data;
using BookStoreAPI.Dtos;
using BookStoreAPI.Helpers;
using BookStoreAPI.services;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly DataContextDapper _dapper;
        private readonly AuthHelper _authHelper;
        private readonly int passwordLength = 8;
        private readonly EmailSender _emailSender;
        public AuthController(IConfiguration config)
        {
            _dapper = new DataContextDapper(config);
            _authHelper = new AuthHelper(config);
            _emailSender = new EmailSender(config);
        }

        [HttpPost("SignUp")]

        public IActionResult SignUp(UserForRegistrationDto user)
        {
            if (!_authHelper.IsValidEmail(user.Email))
                return BadRequest("Please provide a valid email address.");

            if (!user.Password.Equals(user.PasswordConfirm))
                return BadRequest("Password do not match. Please provide a matching password");


            if (user.Password.Length < this.passwordLength)
                return BadRequest("Password must be at least 8 characters long.");



            string sqlCheckUserExist = @$"SELECT Email FROM UserSchema.Auth
                WHERE Email = '{user.Email}'";

            IEnumerable<String> existingUsers = _dapper.LoadData<string>(sqlCheckUserExist);

            if (existingUsers.Count() != 0)
            {
                return BadRequest("User with this email already exists");
            }
            UserForLoginDto userForSetPassword = new UserForLoginDto()
            {
                Email = user.Email,
                Password = user.Password,
            };

            if (_authHelper.SetPassword(userForSetPassword))
            {

                string createNewUserSql = @"EXECUTE UserSchema.spUser_Insert ";

                string sqlParamString = @" @FirstName = @FirstNameParam,
                        @lastName = @LastNameParam,
                        @Email = @EmailParam,
                        @Gender = @GenderParam";

                DynamicParameters sqlParams = new DynamicParameters();
                sqlParams.Add("@FirstNameParam", user.FirstName, DbType.String);
                sqlParams.Add("@LastNameParam", user.LastName, DbType.String);
                sqlParams.Add("@EmailParam", user.Email, DbType.String);
                sqlParams.Add("@GenderParam", user.Gender, DbType.String);
                if (user.PhoneNumber != "")
                {
                    sqlParams.Add("@PhoneNumberParam", user.PhoneNumber, DbType.String);
                    sqlParamString += ", @PhoneNumber = @phoneNumberParam";
                }
                createNewUserSql += sqlParamString;
                if (_dapper.ExecuteSqlWithParameters(createNewUserSql, sqlParams))
                {
                    using (FileStream fileStream = new FileStream("emailTemplate.html", FileMode.Open, FileAccess.Read))
                    {
                        using (StreamReader streamReader = new StreamReader(fileStream))
                        {
                            _emailSender.SendEmailAsync(user.Email, "Welcome to Reading club!", streamReader.ReadToEnd().Replace("{{name}}", user.FirstName));
                        }
                    }

                    return Ok();
                }
                throw new Exception("Failed to add user");
            }
            throw new Exception("Failed to register user.");
        }

        [HttpPost("Login")]
        public IActionResult Login(UserForLoginDto userForLogin)
        {
            if (userForLogin == null)
            {
                return BadRequest("Invalid user data");
            }

            string sqlForHashAndSalt = @"EXEC UserSchema.spLoginConfirmation_Get @Email = @EmailParam";

            DynamicParameters sqlParameters = new DynamicParameters();
            sqlParameters.Add("@EmailParam", userForLogin.Email, DbType.String);

            UserForLoginConfirmationDto userForConfirmation = null;
            try
            {
                userForConfirmation = _dapper.LoadDataSingleWithParameters<UserForLoginConfirmationDto>(sqlForHashAndSalt, sqlParameters);
            }
            catch
            {
                return BadRequest("User not found");
            }

            if (userForConfirmation == null)
            {
                return BadRequest("User not found");
            }

            // Check if the password hash and salt are null
            if (userForConfirmation.PasswordHash == null || userForConfirmation.PasswordSalt == null)
            {
                return StatusCode(401, "User credentials are invalid");
            }

            byte[] passwordHash = _authHelper.GetPasswordHash(userForLogin.Password, userForConfirmation.PasswordSalt);

            if (passwordHash == null || passwordHash.Length != userForConfirmation.PasswordHash.Length)
            {
                return StatusCode(401, "Incorrect Password");
            }

            for (int i = 0; i < passwordHash.Length; i++)
            {
                if (passwordHash[i] != userForConfirmation.PasswordHash[i])
                    return StatusCode(401, "Incorrect Password");
            }

            string userIdSql = $@"SELECT [Id] FROM UserSchema.Users WHERE Email = '{userForLogin.Email}'";
            string userRoleSql = $@"SELECT [UserRole] FROM UserSchema.Users WHERE Email = '{userForLogin.Email}'";

            int userId;
            string userRole;
            try
            {
                userId = _dapper.LoadDataSingle<int>(userIdSql);
                userRole = _dapper.LoadDataSingle<string>(userRoleSql);
            }
            catch (Exception ex)
            {
                // Log the exception for investigation
                return StatusCode(500, $"An error occurred while retrieving user data {ex.Message}");
            }

            if (userId == default(int) || string.IsNullOrEmpty(userRole))
            {
                return StatusCode(500, "Invalid user data retrieved from the database");
            }

            return Ok(new Dictionary<string, string>
    {
        {"token",_authHelper.CreateToken(userId,userRole)}
    });
        }


        [HttpPost("ForgotPassword")]
        public IActionResult ForgotPassword(UserForForgotPasswordDto userForForgotPassword)
        {
            string sqlForEmail = @"SELECT Email FROM UserSchema.Auth
            WHERE Email = @EmailParam";
            DynamicParameters sqlParameters = new DynamicParameters();
            sqlParameters.Add("@EmailParam", userForForgotPassword.Email, DbType.String);

            var email = _dapper.LoadDataSingleWithParameters<string>(sqlForEmail, sqlParameters);


            if (email == null)
                return BadRequest("User with this email does not exist");
            string resetToken;

            try
            {
                resetToken = _authHelper.CreatePasswordResetToken(email);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }


            using (FileStream fileStream = new FileStream("resetPasswordTemplate.html", FileMode.Open, FileAccess.Read))
            {
                using (StreamReader streamReader = new StreamReader(fileStream))
                {
                    var url = $"{this.Request.Scheme}://{this.Request.Host}/{resetToken}";

                    _emailSender.SendEmailAsync(email, "Password Reset (only valid for 5 minutes)", streamReader.ReadToEnd().Replace("{{resetToken}}", url));
                }
            }

            return Ok("Password reset link has been sent to your email");
        }

        [HttpPost("ResetPassword")]
        public IActionResult ResetPassword(UserForResetPasswordDto userForResetPassword)
        {
            if (userForResetPassword.Password == null || !userForResetPassword.Password.Equals(userForResetPassword.PasswordConfirm))
                return BadRequest("Passwords do not match");

            if (userForResetPassword.Password.Length < this.passwordLength)
                return BadRequest("Password must be at least 8 characters long");

            if (userForResetPassword.Token == null)
                return BadRequest("Invalid token");

            var hashedToken = _authHelper.HashString(userForResetPassword.Token);


            string sqlForToken = @"SELECT ResetPasswordToken,ExpiresAt FROM UserSchema.Auth
            WHERE ResetPasswordToken = @ResetPasswordTokenParam";

            DynamicParameters sqlParameters = new DynamicParameters();
            sqlParameters.Add("@ResetPasswordTokenParam", hashedToken, DbType.String);


            var tokenAndExpiration = _dapper.LoadDataSingleWithParameters<TokenAndExpirationDto>(sqlForToken, sqlParameters);

            if (tokenAndExpiration == null)
                return BadRequest("Invalid token");

            if (tokenAndExpiration.ExpiresAt < DateTime.Now)
                return BadRequest("Token has expired");

            if (tokenAndExpiration.ResetPasswordToken != _authHelper.HashString(userForResetPassword.Token))
                return BadRequest("Invalid token");

            var passwordSalt = _authHelper.PasswordSalt();
            var passwordHash = _authHelper.GetPasswordHash(userForResetPassword.Password, passwordSalt);


            string sqlForResetPassword = @"UPDATE UserSchema.Auth
            SET PasswordHash = @PasswordHashParam,
            passwordSalt = @PasswordSaltParam,
            ResetPasswordToken = NULL,
            ExpiresAt = NULL
            WHERE ResetPasswordToken = @ResetPasswordTokenParam";

            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("@PasswordHashParam", passwordHash, DbType.Binary);
            sqlParams.Add("@PasswordSaltParam", passwordSalt, DbType.Binary);
            sqlParams.Add("@ResetPasswordTokenParam", hashedToken, DbType.String);

            if (_dapper.ExecuteSqlWithParameters(sqlForResetPassword, sqlParams))
                return Ok("Password has been reset");

            return BadRequest("Failed to reset password");

        }
    }
}


