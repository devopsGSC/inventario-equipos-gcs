using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;

namespace InventarioTI.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    public EmailService(IConfiguration config, IWebHostEnvironment env) { _config = config; _env = env; }

    // logoCid: si se provee, se busca wwwroot/images/gcs_logo.png y se adjunta como
    // recurso embebido (Content-ID) para que la imagen se muestre sin depender de una
    // URL pública alcanzable por el cliente de correo (localhost no lo es).
    public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, bool incluirLogo = false)
    {
        var from = _config["Email:From"]
            ?? throw new InvalidOperationException("Falta configurar Email:From en appsettings.Development.json.");
        var password = _config["Email:AppPassword"]
            ?? throw new InvalidOperationException("Falta configurar Email:AppPassword en appsettings.Development.json.");
        var host = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
        var port = int.Parse(_config["Email:SmtpPort"] ?? "587");

        var mensaje = new MimeMessage();
        mensaje.From.Add(MailboxAddress.Parse(from));
        mensaje.To.Add(MailboxAddress.Parse(destinatario));
        mensaje.Subject = asunto;

        var builder = new BodyBuilder { HtmlBody = cuerpoHtml };
        if (incluirLogo)
        {
            var logoPath = Path.Combine(_env.WebRootPath, "images", "gcs_logo.png");
            if (File.Exists(logoPath))
            {
                var imagen = builder.LinkedResources.Add(logoPath);
                imagen.ContentId = MimeUtils.GenerateMessageId();
                builder.HtmlBody = builder.HtmlBody.Replace("cid:logo", $"cid:{imagen.ContentId}");
            }
        }
        mensaje.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(from, password);
        await client.SendAsync(mensaje);
        await client.DisconnectAsync(true);
    }

    // Plantilla con la misma identidad visual del sitio (navy + azul de acento),
    // para correos transaccionales con un botón de acción (ej. restablecer contraseña).
    // El logo se referencia como "cid:logo" y EnviarAsync(incluirLogo: true) lo reemplaza
    // por el Content-ID real del recurso embebido.
    public static string PlantillaBoton(string saludo, string mensaje, string urlBoton, string textoBoton) => $"""
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:0;background:#f4f6fa;font-family:'Segoe UI',Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6fa;padding:32px 16px;">
            <tr>
              <td align="center">
                <table role="presentation" width="100%" style="max-width:480px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5eaf2;">
                  <tr>
                    <td style="background:#f8fafc;padding:24px 32px;text-align:center;border-bottom:1px solid #eef0f6;">
                      <img src="cid:logo" alt="GCS" style="height:32px;"/>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:32px;">
                      <p style="margin:0 0 16px;font-size:16px;font-weight:600;color:#1a2540;">{saludo}</p>
                      <p style="margin:0 0 24px;font-size:14px;line-height:1.6;color:#4a5f7a;">{mensaje}</p>
                      <table role="presentation" cellpadding="0" cellspacing="0">
                        <tr>
                          <td style="border-radius:8px;background:#3b82f6;">
                            <a href="{urlBoton}" style="display:inline-block;padding:12px 28px;font-size:14px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:8px;">{textoBoton}</a>
                          </td>
                        </tr>
                      </table>
                      <p style="margin:24px 0 0;font-size:12.5px;color:#8fa3be;">
                        Si el botón no funciona, copia y pega este enlace en tu navegador:<br/>
                        <a href="{urlBoton}" style="color:#3b82f6;word-break:break-all;">{urlBoton}</a>
                      </p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:16px 32px;background:#f8fafc;border-top:1px solid #eef0f6;text-align:center;">
                      <p style="margin:0;font-size:11.5px;color:#8fa3be;">InventarioTI — Global Customs Solutions</p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
}
