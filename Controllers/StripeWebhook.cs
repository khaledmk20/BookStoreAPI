using System.Data;
using System.Security.Claims;
using System.Text.Json;
using BookStoreAPI.Data;
using BookStoreAPI.Dtos;
using BookStoreAPI.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Route("webhooks/stripe")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class StripeWebhookController : ControllerBase
    {
        private readonly DataContextDapper _dapper;
        public StripeWebhookController(IConfiguration config)
        {
            _dapper = new DataContextDapper(config);
        }
        const string endpointSecret = "whsec_58794443d878ce4211193ae856e282a9920d37803ff18d1d7733a98514b19753";

        [HttpPost]
        public async Task<IActionResult> Index()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json,
                    Request.Headers["Stripe-Signature"], endpointSecret);

                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {

                    var session = stripeEvent.Data.Object as Session;
                    var sessionId = session?.Id;
                    var userId = session?.Metadata["userId"];

                    string sql = @"EXEC UserSchema.sp_Order_updateStatusSessionId 
                        @UserId = @UserIdParam,
                        @SessionId = @SessionIdParam,
                        @OrderStatus = @OrderStatusParam";

                    DynamicParameters sqlParams = new DynamicParameters();
                    sqlParams.Add("@UserIdParam", userId, DbType.Int32);
                    sqlParams.Add("@SessionIdParam", sessionId, DbType.String);
                    sqlParams.Add("@OrderStatusParam", "Paid", DbType.String);

                    if (!_dapper.ExecuteSqlWithParameters(sql, sqlParams))
                        return BadRequest("Failed to update order status");

                    return Ok();


                }
                else if (stripeEvent.Type == Events.CheckoutSessionExpired || stripeEvent.Type == Events.PaymentIntentCanceled || stripeEvent.Type == Events.PaymentIntentPaymentFailed || stripeEvent.Type == Events.ChargeFailed)
                {
                    var session = stripeEvent.Data.Object as Session;
                    var sessionId = session?.Id;

                    var userId = session?.Metadata["userId"];

                    string sql = @"EXEC UserSchema.sp_Order_delete 
                            @UserId = @UserIdParam,
                            @SessionId = @SessionIdParam";


                    DynamicParameters sqlParams = new DynamicParameters();
                    sqlParams.Add("@UserIdParam", userId, DbType.Int32);
                    sqlParams.Add("@SessionIdParam", sessionId, DbType.String);
                    if (!_dapper.ExecuteSqlWithParameters(sql, sqlParams))
                        return BadRequest("Failed to delete order");

                    return Ok();

                }
                else
                {
                    Console.WriteLine("Unhandled event type: {0}", stripeEvent.Type);
                }

                return Ok();
            }
            catch (StripeException e)
            {
                return BadRequest(e.Message);
            }
        }













        [NonAction]
        public IActionResult PlaceOrder(OrderForCreationDto orderForCreation)
        {
            var userId = User.FindFirstValue("userId");
            string sql = @"UserSchema.sp_Order_insert 
                    @UserId = @UserIdParam,
                    @OrderLines = @OrderLinesParam";


            string orderLineJson = JsonSerializer.Serialize(orderForCreation.OrderLine);  // Convert to JSON string
            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("@UserIdParam", userId, DbType.Int32);
            sqlParams.Add("@OrderLinesParam", orderLineJson, DbType.String);


            if (!_dapper.ExecuteSqlWithParameters(sql, sqlParams))
                return BadRequest("Failed to place order");

            return Ok();
        }
    }
}