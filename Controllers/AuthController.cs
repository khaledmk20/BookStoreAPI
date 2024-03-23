using System.Data;
using BookStoreAPI.Data;
using BookStoreAPI.Dtos;
using BookStoreAPI.Helpers;
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
        public AuthController(IConfiguration config)
        {
            _dapper = new DataContextDapper(config);
            _authHelper = new AuthHelper(config);
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
                    return Ok();
                }
                throw new Exception("Failed to add user");
            }
            throw new Exception("Failed to register user.");
        }

        [HttpPost("Login")]
        public IActionResult Login(UserForLoginDto userForLogin)
        {
            string sqlForHashAndSalt = @"EXEC UserSchema.spLoginConfirmation_Get 
            @Email = @EmailParam";

            DynamicParameters sqlParameters = new DynamicParameters();

            sqlParameters.Add("@EmailParam", userForLogin.Email, DbType.String);
            UserForLoginConfirmationDto userForConfirmation;
            try
            {
                userForConfirmation = _dapper
               .LoadDataSingleWithParameters<UserForLoginConfirmationDto>(sqlForHashAndSalt, sqlParameters);
            }
            catch
            {
                return BadRequest("User not found");
            }

            byte[] passwordHash = _authHelper.GetPasswordHash(userForLogin.Password, userForConfirmation.PasswordSalt);

            for (int i = 0; i < passwordHash.Length; i++)
            {
                if (passwordHash[i] != userForConfirmation.PasswordHash[i])
                    return StatusCode(401, "Incorrect Password");
            }

            string userIdSql = $@"SELECT [Id] FROM UserSchema.Users
             where  Email = '{userForLogin.Email}' ";
            string userRoleSql = $@"SELECT [UserRole] FROM UserSchema.Users
             where  Email = '{userForLogin.Email}' ";
            int userId = _dapper.LoadDataSingle<int>(userIdSql);
            string userRole = _dapper.LoadDataSingle<string>(userRoleSql);

            return Ok(new Dictionary<string, string>
            {
                {"token",_authHelper.CreateToken(userId,userRole)}
            });

        }

    }
}


