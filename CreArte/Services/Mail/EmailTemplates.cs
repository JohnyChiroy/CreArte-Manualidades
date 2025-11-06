using System;

namespace CreArte.Services.Mail
{
    public static class EmailTemplates
    {
        public static string BuildRecoveryEmailHtml(string nombre, string url, DateTime expiraLocal, string? logoUrl = null)
        {
            string esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var vence = esc(expiraLocal.ToString("dd/MM/yyyy HH:mm"));

            // Paleta CreArte (puedes ajustar)
            const string brand = "#BF265E";   // magenta CreArte (títulos)
            const string accent = "#C5D930";  // lima del botón
            const string text = "#333";
            const string muted = "#6c757d";
            const string bg = "#f7f7f7";
            const string card = "#ffffff";
            const string border = "#eee";

            var logo = string.IsNullOrWhiteSpace(logoUrl) ? "" :
                $"<img src='{esc(logoUrl)}' alt='CreArte' width='40' height='40' " +
                "style='display:block;border:0;border-radius:8px;margin-right:8px;'/>";

            return $@"
<!doctype html>
<html lang='es'>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'></head>
<body style='margin:0;background:{bg};font-family:Segoe UI,Arial,sans-serif;color:{text};'>
  <center style='width:100%;padding:24px 12px;background:{bg};'>
    <table role='presentation' width='600' style='max-width:600px;background:{card};border:1px solid {border};border-radius:12px;overflow:hidden'>
      <tr>
        <td style='padding:16px 20px;display:flex;align-items:center'>
          {logo}
          <div>
            <div style='font-size:20px;font-weight:700;color:{brand}'>CreArte Manualidades</div>
            <div style='font-size:12px;color:{muted}'>Seguridad de cuenta</div>
          </div>
        </td>
      </tr>
      <tr>
        <td style='padding:20px'>
          <h1 style='margin:0 0 8px 0;font-size:22px;color:{brand}'>Restablecer tu contraseña</h1>
          <p style='margin:0 0 12px 0'>Hola {(string.IsNullOrWhiteSpace(nombre) ? "" : $"<strong>{esc(nombre)}</strong>")},</p>
          <p style='margin:0 0 16px 0'>
            Recibimos una solicitud para restablecer tu contraseña. Si no fuiste tú, ignora este mensaje.
          </p>
          <p style='text-align:center;margin:24px 0'>
            <a href='{esc(url)}' style='display:inline-block;background:{accent};color:#000;padding:12px 18px;border-radius:8px;text-decoration:none;font-weight:700'>
              Restablecer contraseña
            </a>
          </p>
          <p style='font-size:12px;color:{muted};margin:0 0 10px 0'>
            Si el botón no funciona, copia y pega este enlace en tu navegador:
          </p>
          <p style='word-break:break-all;margin:0 0 16px 0'>
            <a href='{esc(url)}' style='color:#0d6efd;text-decoration:none'>{esc(url)}</a>
          </p>
          <p style='font-size:12px;color:{muted};margin:0'>
            Este enlace vence el <strong>{vence}</strong>.
          </p>
        </td>
      </tr>
      <tr>
        <td style='background:#fafafa;border-top:1px solid {border};padding:14px 20px'>
          <div style='font-size:12px;color:{muted}'>Este es un mensaje automático. No responda a este correo.</div>
        </td>
      </tr>
    </table>
    <div style='font-size:11px;color:{muted};margin-top:10px'>© {DateTime.Now.Year} CreArte Manualidades</div>
  </center>
</body>
</html>";
        }
    }
}
