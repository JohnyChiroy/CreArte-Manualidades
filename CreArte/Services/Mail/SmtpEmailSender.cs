using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CreArte.Services.Mail
{
    public class SmtpOptions
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty; // OJO: coincide con appsettings
        public string FromName { get; set; } = "CreArte Manualidades";
    }

    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);
    }

    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpOptions _opt;
        public SmtpEmailSender(IOptions<SmtpOptions> opt) => _opt = opt.Value;

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            using var msg = new MailMessage();
            msg.From = new MailAddress(_opt.FromAddress, _opt.FromName);
            msg.To.Add(new MailAddress(toEmail));
            msg.Subject = subject ?? "";
            msg.Body = htmlBody ?? "";
            msg.IsBodyHtml = true;

            using var smtp = new SmtpClient(_opt.Host, _opt.Port)
            {
                EnableSsl = _opt.EnableSsl,
                Credentials = new NetworkCredential(_opt.User, _opt.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            await smtp.SendMailAsync(msg);
        }
    }
}
