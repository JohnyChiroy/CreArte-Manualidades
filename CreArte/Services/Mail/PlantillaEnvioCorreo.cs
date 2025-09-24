using System;

namespace CreArte.Services.Mail
{
    public class PlantillaEnvioCorreo
    {
        // Genera un HTML simple con botón para restablecer
        public string GenerarHtmlRecuperacion(string usuarioNombre, string url, DateTime expiraUtc)
        {
            // Nota: expiraUtc viene en UTC; puedes mostrarlo en tu huso si lo prefieres
            return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
  <meta charset='utf-8' />
  <title>Recuperación de contraseña</title>
</head>
<body style='font-family:Arial, Helvetica, sans-serif; background:#f7f7f7; padding:20px;'>
  <div style='max-width:600px; margin:0 auto; background:#ffffff; border-radius:10px; padding:24px; box-shadow:0 4px 16px rgba(0,0,0,.06);'>
    <h2 style='margin-top:0; color:#BF265E;'>CreArte Manualidades</h2>
    <p>Hola <strong>{Esc(usuarioNombre)}</strong>,</p>
    <p>Hemos recibido una solicitud para restablecer tu contraseña. Haz clic en el botón de abajo para crear una nueva contraseña.</p>
    <p style='text-align:center; margin:28px 0;'>
      <a href='{Esc(url)}' 
         style='background:#C5D930; color:#000; text-decoration:none; padding:12px 20px; border-radius:8px; font-weight:700; display:inline-block;'>
         Restablecer contraseña
      </a>
    </p>
    <p>Este enlace caduca en 1 hora. Si tú no solicitaste este cambio, puedes ignorar este correo.</p>
    <hr style='border:0; border-top:1px solid #eee; margin:24px 0;' />
    <p style='font-size:12px; color:#888;'>Si el botón no funciona, copia y pega este enlace en tu navegador:<br>{Esc(url)}</p>
  </div>
</body>
</html>";
        }

        // Escapar básico (evita inyección en HTML)
        private static string Esc(string? s) =>
            System.Net.WebUtility.HtmlEncode(s ?? "");
    }
}
