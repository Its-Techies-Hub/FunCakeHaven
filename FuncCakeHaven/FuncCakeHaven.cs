using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace CakeHaven
{
    public class FuncCakeHaven
    {
        private readonly ILogger<FuncCakeHaven> _logger;

        public FuncCakeHaven(ILogger<FuncCakeHaven> logger)
        {
            _logger = logger;
        }
       

        [Function("PostOrder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            try
            {
                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var order = JsonConvert.DeserializeObject<Order>(requestBody);

                if (order == null)
                {
                    return new JsonResult(new { message = "Invalid order data.", status = "error" }) { StatusCode = 400 };
                }

                // Database connection string (replace with your actual connection string)
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                // Insert order into the database
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Check if the order already exists (this is optional and can be adjusted based on your needs)
                    string checkOrderQuery = "SELECT COUNT(*) FROM orders WHERE customerEmail = @customerEmail AND deliveryDate = @deliveryDate";
                    using (SqlCommand checkCmd = new SqlCommand(checkOrderQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@customerEmail", order.CustomerEmail);
                        checkCmd.Parameters.AddWithValue("@deliveryDate", order.DeliveryDate);

                        int existingOrders = (int)await checkCmd.ExecuteScalarAsync();
                        if (existingOrders > 0)
                        {
                            return new JsonResult(new { message = "An order for this email and delivery date already exists.", status = "error" }) { StatusCode = 409 };
                        }
                    }

                    // Proceed with inserting the new order
                    string insertQuery = "INSERT INTO orders (customerName, customerEmail, customerPhone, cakeType, cakeSize, deliveryDate, specialInstructions, orderStatus, orderDate) " +
                                         "VALUES (@customerName, @customerEmail, @customerPhone, @cakeType, @cakeSize, @deliveryDate, @specialInstructions, @orderStatus, @orderDate)";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        // Add parameters to prevent SQL injection
                        cmd.Parameters.AddWithValue("@customerName", order.CustomerName);
                        cmd.Parameters.AddWithValue("@customerEmail", order.CustomerEmail);
                        cmd.Parameters.AddWithValue("@customerPhone", order.CustomerPhone);
                        cmd.Parameters.AddWithValue("@cakeType", order.CakeType);
                        cmd.Parameters.AddWithValue("@cakeSize", order.CakeSize);
                        cmd.Parameters.AddWithValue("@deliveryDate", order.DeliveryDate);
                        cmd.Parameters.AddWithValue("@specialInstructions", order.SpecialInstructions);
                        cmd.Parameters.AddWithValue("@orderStatus", order.OrderStatus ?? "Pending"); // Default to "Pending" if no status provided
                        cmd.Parameters.AddWithValue("@orderDate", DateTime.UtcNow); // Set the order date to the current timestamp

                        // Execute the insert command
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            _logger.LogInformation($"Order successfully added for {order.CustomerName}.");
                        }
                        else
                        {
                            _logger.LogError("Failed to insert the order into the database.");
                        }
                    }
                }

                var response = new
                {
                    message = $"Order for {order.CustomerName} has been successfully placed.",
                    status = "success"
                };

                return new JsonResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error inserting order: {ex.Message}");
                var errorResponse = new
                {
                    message = "An error occurred while processing your order.",
                    error = ex.Message
                };
                return new JsonResult(errorResponse) { StatusCode = 500 };
            }
        }


        // Define a model class for the order
        public class Order
        {
            public string CustomerName { get; set; }
            public string CustomerEmail { get; set; }
            public string CustomerPhone { get; set; }
            public string CakeType { get; set; }
            public string CakeSize { get; set; }
            public DateTime DeliveryDate { get; set; }
            public string SpecialInstructions { get; set; }
            public string OrderStatus { get; set; }
        }
    }
}
