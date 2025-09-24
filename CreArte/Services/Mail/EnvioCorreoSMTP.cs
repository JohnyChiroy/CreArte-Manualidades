using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace CreArte.Services.Mail
{
    public class SmtpOptions
    {
        public string Host { get; set; }
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string User { get; set; }
        public string Password { get; set; }
        public string From { get; set; }
        public string FromName { get; set; }
    }

    public class EnvioCorreoSMTP
    {
        private readonly SmtpOptions _opt;
        private readonly ILogger<EnvioCorreoSMTP> _log;

        public EnvioCorreoSMTP(IOptions<SmtpOptions> options, ILogger<EnvioCorreoSMTP> log)
        {
            _opt = options.Value;
            _log = log;
        }

        /// <summary>
        /// Envía un correo HTML. Retorna (ok, errorMessage).
        /// En producción: registra el detalle en logs. En dev podrás mostrar el error.
        /// </summary>
        public async Task<(bool ok, string? error)> EnviarAsync(string to, string subject, string htmlBody)
        {
            try
            {
                // 0) Validaciones defensivas
                if (string.IsNullOrWhiteSpace(_opt.From)) return (false, "SMTP.From vacío.");
                if (string.IsNullOrWhiteSpace(_opt.User)) return (false, "SMTP.User vacío.");
                if (string.IsNullOrWhiteSpace(to)) return (false, "Destino vacío.");

                // 1) Normaliza y valida
                to = to.Trim();
                var fromAddr = new MailAddress(_opt.From.Trim(), _opt.FromName);
                var toAddr = new MailAddress(to); // <- si es inválido, aquí revienta con mensaje claro

                using var msg = new MailMessage();
                msg.From = fromAddr;
                msg.To.Add(toAddr);
                msg.Subject = subject ?? "";
                msg.Body = htmlBody ?? "";
                msg.IsBodyHtml = true;

                using var client = new SmtpClient(_opt.Host, _opt.Port)
                {
                    EnableSsl = _opt.EnableSsl,
                    Credentials = new NetworkCredential(_opt.User, _opt.Password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                await client.SendMailAsync(msg);
                return (true, null);
            }
            catch (SmtpException ex) { return (false, $"SMTP: {ex.StatusCode} - {ex.Message}"); }
            catch (Exception ex) { return (false, ex.Message); }
        }
    }
}
