using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MySql.Data.MySqlClient;
using SendGrid;
using SendGrid.Helpers.Mail;
using PHARMACY_API.Model;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PHARMACY_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        // GET: api/<AuthController>
        private readonly IConfiguration _config;

        public AuthController(IConfiguration config)
        {
            _config = config;
        }
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        
        

        

        [HttpPost]
        public IActionResult Login([FromBody] Models.LoginModel login)
        {
            if (login == null)
            {
                return BadRequest("Invalid client request");
            }

            // Query the database for the specified username
            using (var connection = new MySqlConnection(_config["ConnectionStrings:DefaultConnection"]))
            {
                try
                {

                    connection.Open();
                }
                catch (Exception ex) { throw; }

                var command = new MySqlCommand("SELECT password, role FROM Users WHERE username = @username", connection);
                command.Parameters.AddWithValue("@username", login.Username);

                var reader = command.ExecuteReader();

                if (!reader.HasRows)
                {
                    return Unauthorized();
                }

                reader.Read();

                var hashedPassword = reader.GetString("password");
                var role = reader.GetString("role");

                
                var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]));
                var signinCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

                var tokeOptions = new JwtSecurityToken(
                    issuer: _config["Jwt:Issuer"],
                    audience: _config["Jwt:Audience"],
                    claims: new[] { new Claim("id", "1"), new Claim(ClaimTypes.Role, role) },
                    expires: DateTime.Now.AddMinutes(5),
                    signingCredentials: signinCredentials
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(tokeOptions);
                return Ok(new { Token = tokenString, Role=role });
            }
        }
        private bool VerifyPassword(string password, string hashedPassword)
        {
            // Verify the specified password against the stored hashed password
            // For example, using the BCrypt algorithm:
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }



        [HttpPost("register")]
        public IActionResult Register([FromBody] Models.UserModel register)
        {
            if (register == null)
            {
                return BadRequest("Invalid client request");
            }

            using (var connection = new MySqlConnection(_config["ConnectionStrings:DefaultConnection"]))
            {
                try
                {
                    connection.Open();
                }
                catch (Exception ex)
                {
                    throw;
                }

                // Check if username already exists
                var command = new MySqlCommand("SELECT COUNT(*) FROM Users WHERE username = @username", connection);
                command.Parameters.AddWithValue("@username", register.Username);

                int count = Convert.ToInt32(command.ExecuteScalar());
                if (count > 0)
                {
                    return BadRequest("Username already exists");
                }

                // Check if email already registered
                command = new MySqlCommand("SELECT COUNT(*) FROM Users WHERE email = @email", connection);
                command.Parameters.AddWithValue("@email", register.Email);

                count = Convert.ToInt32(command.ExecuteScalar());
                if (count > 0)
                {
                    return BadRequest("Email already registered");
                }

                // Insert new user if username and email are available
                //var hashedPassword = HashPassword(register.Password);
                command = new MySqlCommand("INSERT INTO Users (username, email, password, role) VALUES (@username, @email, @password, @role)",
                                            connection);
                command.Parameters.AddWithValue("@username", register.Username);
                command.Parameters.AddWithValue("@email", register.Email);
                command.Parameters.AddWithValue("@password", register.Password);
                command.Parameters.AddWithValue("@role", "user");

                command.ExecuteNonQuery();

                // Generate JWT token and send response with role
                var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]));
                var signinCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

                var tokeOptions = new JwtSecurityToken(
                    issuer: _config["Jwt:Issuer"],
                    audience: _config["Jwt:Audience"],
                    claims: new[] { new Claim("id", "1"), new Claim(ClaimTypes.Role, "user") },
                    expires: DateTime.Now.AddMinutes(5),
                    signingCredentials: signinCredentials
                );

                
                return Ok();
            }
        }
        private async Task SendRegistrationEmailAsync(string email)
        {
            var apiKey = _config["SendGrid:ApiKey"];
            var client = new SendGridClient(apiKey);

            var from = new EmailAddress("noreply@example.com", "Example App");
            var subject = "Welcome to Example App!";
            var to = new EmailAddress(email);
            var plainTextContent = "Thank you for registering on Example App.";
            var htmlContent = "<p>Thank you for registering on <b>Example App</b>.</p>";
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            var response = await client.SendEmailAsync(msg);

            if (response.StatusCode != System.Net.HttpStatusCode.OK && response.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                // log or handle error
            }
        }


    }
}
