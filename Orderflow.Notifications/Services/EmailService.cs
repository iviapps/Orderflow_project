using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Orderflow.Notifications.Services;

public class EmailService(IConfiguration configuration, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendWelcomeEmailAsync(string toEmail, string? firstName, CancellationToken cancellationToken = default)
    {
        var displayName = firstName ?? toEmail.Split('@')[0];

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            configuration["Email:FromName"] ?? "OrderFlow",
            configuration["Email:FromAddress"] ?? "noreply@orderflow.local"));

        message.To.Add(new MailboxAddress(displayName, toEmail));
        message.Subject = "Welcome to OrderFlow!";

        message.Body = new TextPart("html")
        {
            Text = $"""
            <html>
              <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                <h1 style="color: #333;">Welcome to OrderFlow!</h1>
                <p>Hi {displayName},</p>
                <p>Thank you for registering with OrderFlow. Your account has been successfully created.</p>
                <p>You can now log in and start using our platform.</p>
                <br/>
                <p>Best regards,<br/>The OrderFlow Team</p>
              </body>
            </html>
            """
        };

        await SendEmailAsync(message, cancellationToken);

        logger.LogInformation("Welcome email sent to {Email}", toEmail);
    }

    private async Task SendEmailAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var smtpHost = configuration["Email:SmtpHost"] ?? "localhost";
        var smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? "1025");

        var socketOptions = SecureSocketOptions.Auto;

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(smtpHost, smtpPort, socketOptions, cancellationToken); // :contentReference[oaicite:3]{index=3}

            // Si tu SMTP requiere auth:
            // var user = configuration["Email:Username"];
            // var pass = configuration["Email:Password"];
            // if (!string.IsNullOrWhiteSpace(user))
            //     await client.AuthenticateAsync(user, pass, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipients}", string.Join(", ", message.To));
            throw;
        }
    }
}
