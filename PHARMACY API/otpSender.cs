using Microsoft.Win32;
using static System.Net.WebRequestMethods;
using System.Net.Mail;
using System.Net;

namespace PHARMACY_API
{
    public class otpSender
    {
        public void SendOtp(string to,int otp)
        {
            string subject = "Your OTP for PHARMACY API";
            string message = "Thank you for registering with PHARMACY API!";

            string From ="ahmad.sami2009@hotmail.com";

            var smtpClient = new SmtpClient
            {
                Host = "smtp-mail.outlook.com",
                Port = 587,
                Credentials = new NetworkCredential("ahmad.sami2009@hotmail.com", "gktxouimjlklvhbo"),
                UseDefaultCredentials = false,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            using (var mailMessages = new MailMessage(From, to)
            {
                Subject = subject,
                Body = message + " Your OTP is: " + otp
            })
            {
                smtpClient.Send(mailMessages);
            }

                //smtpClient.UseDefaultCredentials = false;
                //smtpClient.Credentials = new NetworkCredential("ahmad.sami@ascot.ws", "qtswknlzgqkutjuo");
                //smtpClient.EnableSsl = true;
                
            
        }
    }
}
