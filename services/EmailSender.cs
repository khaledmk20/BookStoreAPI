using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace BookStoreAPI.services
{
    public class EmailSender
    {
        private readonly IConfiguration _config;
        public EmailSender(IConfiguration config)
        {
            _config = config;

        }
        public Task SendEmailAsync(string email, string subject, string body)
        {

            var mail = _config["Gmail:Email"];
            var pw = _config["Gmail:Password"];

            var client = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(mail, pw),
                EnableSsl = true,
            };


            return client.SendMailAsync(new MailMessage()
            {
                To = { new MailAddress(email) },
                From = new MailAddress(mail!),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            });
        }
    }
}