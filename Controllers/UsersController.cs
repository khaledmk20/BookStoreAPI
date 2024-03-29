using System.Data;
using System.Security.Claims;
using System.Text.Json;
using BookStoreAPI.Data;
using BookStoreAPI.Dtos;
using BookStoreAPI.Helpers;
using BookStoreAPI.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly DataContextDapper _dapper;
        private readonly AuthHelper _auth;
        private readonly AzureBlobStorageService _uploadService;

        public UsersController(IConfiguration config)
        {
            _auth = new AuthHelper(config);
            _dapper = new DataContextDapper(config);
            _uploadService = new AzureBlobStorageService(config);

        }

        [HttpGet()]
        public IActionResult GetAllUsers()
        {
            if (!_auth.IsAdmin(User))
                return Unauthorized("Only admins can access this route");


            string sql = @"SELECT [Id],
                    [FirstName],
                    [LastName],
                    [Email],
                    [PhoneNumber],
                    [UserRole],
                    [Gender] 
                FROM UserSchema.Users";

            return Ok(_dapper.LoadData<User>(sql));
        }

        [HttpGet("UserInfo")]
        public IActionResult GetUserInformation()
        {
            var userId = User.FindFirstValue("userId");

            string sql = $@"SELECT [Id],
                    [FirstName],
                    [LastName],
                    [Email],
                    [PhoneNumber],
                    [Gender],
                    [UserImageUrl]
                FROM UserSchema.Users
                WHERE Id = {userId}";
            return Ok(_dapper.LoadDataSingle<SingleUser>(sql));
        }

        [HttpPut("User")]
        public IActionResult EditUser(UserForRegistrationDto userToEdit)
        {
            var userId = User.FindFirstValue("userId");

            string sql = @$"UPDATE UserSchema.Users
                SET Email = '{userToEdit.Email}', 
                FirstName= '{userToEdit.FirstName}',
                LastName = '{userToEdit.LastName}', 
                PhoneNumber = '{userToEdit.PhoneNumber}'
                WHERE Id  = {userId}";

            if (!_dapper.ExecuteSql(sql))
                throw new Exception("Could not update user information");
            return Ok();
        }


        [HttpPost("Address")]
        public IActionResult AddUserAddress(AddressToAddDto addressToAdd)
        {
            var userId = User.FindFirstValue("userId");
            string sql = "EXEC UserSchema.spUserAddress_upsert ";
            string sqlParamString = @" @UserId = @UserIdParam,
                @AddressLine1 = @AddressLine1Param,
                @City = @CityParam,
                @Country = @CountryParam";

            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("@UserIdParam", userId, DbType.Int32);
            sqlParams.Add("@AddressLine1Param", addressToAdd.AddressLine1, DbType.String);
            sqlParams.Add("@CityParam", addressToAdd.City, DbType.String);
            sqlParams.Add("@CountryParam", addressToAdd.Country, DbType.String);

            if (addressToAdd.AddressLine2 is not null)
            {
                sqlParams.Add("@AddressLine2Param", addressToAdd.AddressLine2, DbType.String);
                sqlParamString += ", @AddressLine2 = @AddressLine2Param";
            }
            if (addressToAdd.PostalCode is not null)
            {
                sqlParams.Add("@PostalCodeParam", addressToAdd.PostalCode, DbType.String);
                sqlParamString += ", @PostalCode = @PostalCodeParam";
            }

            sql += sqlParamString;

            if (!_dapper.ExecuteSqlWithParameters(sql, sqlParams))
                return BadRequest("Failed to add address");



            return Ok();
        }

        [HttpPut("Address")]
        public IActionResult EditUserAddress(AddressToEditDto addressToEdit)
        {
            if (addressToEdit.Id == 0)
                return BadRequest("Address Id must be specified");


            var userId = User.FindFirstValue("userId");
            string sql = "EXEC UserSchema.spUserAddress_upsert ";
            string sqlParamString = @"@AddressId = @AddressIdParam,
             @UserId = @UserIdParam,
                @AddressLine1 = @AddressLine1Param,
                @City = @CityParam,
                @Country = @CountryParam";

            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("@UserIdParam", userId, DbType.Int32);
            sqlParams.Add("@AddressIdParam", addressToEdit.Id, DbType.Int32);
            sqlParams.Add("@AddressLine1Param", addressToEdit.AddressLine1, DbType.String);
            sqlParams.Add("@CityParam", addressToEdit.City, DbType.String);
            sqlParams.Add("@CountryParam", addressToEdit.Country, DbType.String);

            if (addressToEdit.AddressLine2 is not null)
            {
                sqlParams.Add("@AddressLine2Param", addressToEdit.AddressLine2, DbType.String);
                sqlParamString += ", @AddressLine2 = @AddressLine2Param";
            }
            if (addressToEdit.PostalCode is not null)
            {
                sqlParams.Add("@PostalCodeParam", addressToEdit.PostalCode, DbType.String);
                sqlParamString += ", @PostalCode = @PostalCodeParam";
            }

            sql += sqlParamString;

            if (!_dapper.ExecuteSqlWithParameters(sql, sqlParams))
                return BadRequest("Failed to edit address");

            return Ok();
        }

        [HttpGet("Address")]
        public IActionResult GetUserAddress()
        {
            var userId = User.FindFirstValue("userId");
            string sql = $@"SELECT [Id],
                [UserId],
                [AddressLine1],
                [AddressLine2],
                [City],
                [PostalCode],
                [Country] FROM UserSchema.UserAddress
                WHERE UserId = {userId}";

            return Ok(_dapper.LoadData<Address>(sql));
        }

        [HttpPost("Cart/{bookId}")]
        public IActionResult AddToCart(int bookId)
        {
            return ExecuteShoppingCartProcedure(bookId, "UserSchema.sp_ShoppingCart_add");
        }

        [HttpDelete("Cart/{bookId}")]
        public IActionResult RemoveFromCart(int bookId)
        {
            return ExecuteShoppingCartProcedure(bookId, "UserSchema.sp_ShoppingCart_remove");
        }

        [NonAction]
        public IActionResult ExecuteShoppingCartProcedure(int bookId, string procedureName)
        {
            var userId = User.FindFirstValue("userId");

            var sql = @$"{procedureName}
                    @UserId = @UserIdParam ,
                    @BookId = @BookIdParam";

            var parameters = new DynamicParameters();
            parameters.Add("@UserIdParam", userId, DbType.Int32);
            parameters.Add("@BookIdParam", bookId, DbType.Int32);

            if (!_dapper.ExecuteSqlWithParameters(sql, parameters))
                return BadRequest("Invalid book ID or user ID");

            return Ok();
        }


        [HttpGet("Cart")]
        public IActionResult GetUserCart()
        {
            var userId = User.FindFirstValue("userId");
            string sql = @"EXEC UserSchema.sp_ShoppingCart_get @UserId = @UserIdParam";
            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("@UserIdParam", userId, DbType.Int32);

            return Ok(_dapper.LoadDataWithParameters<Cart>(sql, sqlParams));
        }

        [HttpGet("Orders")]
        public IActionResult GetOrdersDetails()
        {
            var userId = User.FindFirstValue("userId");
            string sql = @"UserSchema.Orders_get @UserId = @UserIdParam";
            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("@UserIdParam", userId, DbType.Int32);

            var orderDetails = _dapper.LoadDataWithParameters<Order>(sql, sqlParams);


            var groupedOrders = orderDetails.GroupBy(o => o.OrderId).Select(group =>
            {
                var orderGroup = group.ToList();
                return new
                {
                    orderGroup.First().OrderId,
                    orderGroup.First().OrderStatus,
                    orderGroup.First().TotalAmount,
                    orderGroup.First().OrderDate,
                    OrderLines = orderGroup.Select(o => new
                    {
                        o.BookId,
                        o.BookTitle,
                        o.BookImage,
                        o.Quantity,
                        o.Price
                    }).ToList()
                };
            });

            return Ok(groupedOrders);
        }

        [HttpPost("Order")]
        public IActionResult PlaceOrder(OrderForCreationDto orderForCreation)
        {
            if (!_auth.IsAdmin(User))
                return Unauthorized("Only admins can access this route");

            var userId = User.FindFirstValue("userId");
            string sql = @"UserSchema.sp_Order_insert 
                    @UserId = @UserIdParam,
                    @OrderLines = @OrderLinesParam";


            string orderLineJson = JsonSerializer.Serialize(orderForCreation.OrderLine);
            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("@UserIdParam", userId, DbType.Int32);
            sqlParams.Add("@OrderLinesParam", orderLineJson, DbType.String);


            if (!_dapper.ExecuteSqlWithParameters(sql, sqlParams))
                return BadRequest("Failed to place order");

            return Ok();
        }
        [HttpPut("Order/{orderId}")]
        public IActionResult UpdateOrderStatus(int orderId, OrderStatusDto orderStatus)
        {
            string[] validOrderStatus = { "Pending", "Processing", "Confirmed", "Shipped", "Delivered", "On Hold", "Cancelled", "Refunded", };
            if (!validOrderStatus.Contains(orderStatus.OrderStatus, StringComparer.OrdinalIgnoreCase))
                return BadRequest("Invalid order status");

            var userId = User.FindFirstValue("userId");
            string sql = @"EXEC UserSchema.sp_Order_updateStatus 
                @UserId = @UserIdParam,
                @OrderId = @OrderIdParam,
                @OrderStatus = @OrderStatusParam";

            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("@UserIdParam", userId, DbType.Int32);
            sqlParams.Add("@OrderIdParam", orderId, DbType.Int32);
            sqlParams.Add("@OrderStatusParam", orderStatus.OrderStatus, DbType.String);

            if (!_dapper.ExecuteSqlWithParameters(sql, sqlParams))
                return BadRequest("Failed to update order status");

            return Ok();
        }


        [HttpPut("editUserImage")]
        public async Task<IActionResult> UploadUserImage(IFormFile image)
        {

            var userId = User.FindFirstValue("userId");
            if (userId is null)
                return Unauthorized("User not found");
            try
            {
                if (image.Length > 0)
                {
                    string fileURL = await _uploadService.UploadImageAsync(image.OpenReadStream(), image.FileName.Trim(), image.ContentType, true);
                    return updateUserImage(fileURL, userId);
                }
                else
                {
                    return BadRequest();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }

        }



        [NonAction]
        private IActionResult updateUserImage(string fileURL, string userId)
        {
            string sql = @$" UPDATE UserSchema.Users 
                    SET UserImageUrl = @UserImageUrlParam
                    WHERE Id = @UserIdParam";

            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("@UserIdParam", userId, DbType.Int32);
            sqlParams.Add("@UserImageUrlParam", fileURL, DbType.String);
            if (!_dapper.ExecuteSqlWithParameters(sql, sqlParams))
                return BadRequest("Failed to update user image");

            return Ok();
        }
    }
}