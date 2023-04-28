using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MySql.Data.MySqlClient;
using SendGrid;
using SendGrid.Helpers.Mail;
using PHARMACY_API.Model;
using static PHARMACY_API.Model.Models;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Identity.UI.Services;
using BCryptNet = BCrypt.Net.BCrypt;
using System.Data;
using Microsoft.Win32;

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
        //[HttpGet]
        //public IEnumerable<string> Get()
        //{
        //    return new string[] { "value1", "value2" };
        //}
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
                connection.Open();
                var command = new MySqlCommand("SELECT password, role, activated FROM Users WHERE username = @username", connection);
                command.Parameters.AddWithValue("@username", login.Username);
                var reader = command.ExecuteReader();
                if (!reader.HasRows)
                {
                    return Unauthorized();
                }
                reader.Read();
                var hashedPassword = reader.GetString("password");
                var role = reader.GetString("role");
                var activated = reader.GetString("activated");
                var hashedLoginPassword = login.Password;
                reader.Close();
                if (hashedLoginPassword != hashedPassword)
                {
                    return BadRequest();
                }
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
                return Ok(new { Token = tokenString, Role = role, Activated = activated });
            }
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
                command = new MySqlCommand("INSERT INTO Users (username, email, password, role, activated) VALUES (@username, @email, @password, @role, @activated)",
                                            connection);
                command.Parameters.AddWithValue("@username", register.Username);
                command.Parameters.AddWithValue("@email", register.Email);
                command.Parameters.AddWithValue("@password", register.Password);
                command.Parameters.AddWithValue("@role", register.Role);
                command.Parameters.AddWithValue("@activated", "false");


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

                Random random = new Random();
                int otp = random.Next(100000, 999999);
                string otpString = otp.ToString();
                var expiryTime = DateTime.Now.AddMinutes(15);

                command = new MySqlCommand("INSERT INTO otps (username, otp, expiry) VALUES (@username, @otp, @expiry)",
                                    connection);
                command.Parameters.AddWithValue("@username", register.Username);
                command.Parameters.AddWithValue("@otp", otpString);
                command.Parameters.AddWithValue("@expiry", expiryTime);
                command.ExecuteNonQuery();

                otpSender otpsender = new otpSender();
                otpsender.SendOtp(register.Email, otp);


                var tokenString = new JwtSecurityTokenHandler().WriteToken(tokeOptions);
                return Ok();
            }
        }
        [HttpPost("checkotp")]
        public IActionResult CheckOTP([FromBody] Models.CheckotpModel checkotp)
        {
            if (checkotp == null)
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

                var command = new MySqlCommand("SELECT otp, expiry FROM otps WHERE username = @username", connection);
                command.Parameters.AddWithValue("@username", checkotp.Username);

                var reader = command.ExecuteReader();

                if (!reader.HasRows)
                {
                    return BadRequest(new { Failed = "Invalid OTP" });
                }

                reader.Read();

                var otp = reader.GetInt64("otp");
                var expiry = reader.GetDateTime("expiry");

                if (DateTime.Now >= expiry)
                {
                    return BadRequest(new { Failed = "OTP is Expired" });
                }

                if (otp != checkotp.OTP)
                {
                    return BadRequest(new { Failed = "Invalid OTP" });
                }

                return Ok(new { Success = "OTP is valid" });
            }
        }
        [HttpPost("newotp")]
        public IActionResult NewOTP([FromBody] Models.NewOtpModel newOtp)
        {
            if (newOtp == null)
            {
                return BadRequest("Invalid client request");
            }

            using (var connection = new MySqlConnection(_config["ConnectionStrings:DefaultConnection"]))
            {
                connection.Open();
                Random random = new Random();
                int otp = random.Next(100000, 999999);
                string otpString = otp.ToString();
                var expiryTime = DateTime.Now.AddMinutes(15);
                var command = new MySqlCommand("SELECT email FROM Users WHERE username=@username", connection);
                command.Parameters.AddWithValue("@username", newOtp.Username);
                var reader = command.ExecuteReader();
                reader.Read();
                var email = reader.GetString("email");
                reader.Close();
                command = new MySqlCommand("UPDATE otps SET otp = @otp, expiry = @expiry WHERE username = @username\r\n", connection);
                command.Parameters.AddWithValue("@username", newOtp.Username);
                command.Parameters.AddWithValue("@otp", otp);
                command.Parameters.AddWithValue("@expiry", expiryTime);
                command.ExecuteNonQuery();
                otpSender otpsender = new otpSender();
                otpsender.SendOtp(email, otp);
                return Ok();
            }

        }

    }


}
