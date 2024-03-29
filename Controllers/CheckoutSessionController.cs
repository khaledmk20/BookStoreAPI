using System.Data;
using System.Security.Claims;
using System.Text.Json;
using BookStoreAPI.Data;
using BookStoreAPI.Dtos;
using BookStoreAPI.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]

    public class CheckoutSessionController : ControllerBase
    {
        private readonly UsersController _usersController;
        private readonly DataContextDapper _dapper;
        private readonly IConfiguration _config;

        public CheckoutSessionController(IConfiguration config)
        {
            _usersController = new UsersController(config);
            _dapper = new DataContextDapper(config);
            _config = config;
        }


        [HttpPost]
        public string CreateSession(Cart[] cartItems)
        {
            if (cartItems.Length == 0)
                return null;

            var userId = User.FindFirstValue("userId");

            List<SessionLineItemOptions> lineItems = new List<SessionLineItemOptions>();
            foreach (var item in cartItems)
            {
                lineItems.Add(new SessionLineItemOptions
                {

                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmountDecimal = Math.Round(item.BookPrice, 2) * 100,
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.BookTitle,
                            Description = item.BookDescription,
                            Images = item.BookImage != null ? new List<string> { item.BookImage } : null,
                        },
                    },

                    Quantity = Convert.ToInt32(item.Quantity),
                });
            }

            StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
            var domain = _config["AppSettings:Domain"];
            var sessionService = new SessionService();
            DateTimeOffset expirationTime = DateTimeOffset.UtcNow.AddMinutes(30);
            long unixTimestamp = expirationTime.ToUnixTimeSeconds();


            var service = new Stripe.Checkout.SessionService();
            var options = new SessionCreateOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId! }
                },

                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime,
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = lineItems,
                Mode = "payment",
                SuccessUrl = $"{domain}/checkout/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/checkout/cancel",
            };

            Session session = service.Create(options);
            CreateOrder(cartItems, session.Id);

            return session.Url;
        }

        [NonAction]
        public IActionResult CreateOrder(Cart[] cartItems, string sessionId)
        {
            var userId = User.FindFirstValue("userId");

            var orderLines = new List<OrderForCreationDto.OrderLineDto>();

            foreach (var item in cartItems)
            {
                var orderLine = new OrderForCreationDto.OrderLineDto
                {
                    BookId = item.BookId,
                    Quantity = item.Quantity,
                    Price = item.BookPrice,
                };

                orderLines.Add(orderLine);
            }

            var orderForCreation = new OrderForCreationDto();
            orderForCreation.OrderLine = orderLines.ToArray();

            var orderJson = JsonSerializer.Serialize(orderForCreation.OrderLine);

            string sql = @"UserSchema.sp_Order_insert 
                    @UserId = @UserIdParam,
                    @OrderLines = @OrderLinesParam,
                    @SessionId = @SessionIdParam";

            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("@UserIdParam", userId, DbType.Int32);
            sqlParams.Add("@OrderLinesParam", orderJson, DbType.String);
            sqlParams.Add("@SessionIdParam", sessionId, DbType.String);

            if (!_dapper.ExecuteSqlWithParameters(sql, sqlParams))
            {
                return BadRequest();
            }

            return Ok();
        }


    }
}