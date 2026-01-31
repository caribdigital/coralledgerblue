using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Email service implementation using SendGrid
/// </summary>
public class SendGridEmailService : IEmailService
{
    private readonly SendGridOptions _options;
    private readonly ILogger<SendGridEmailService> _logger;
    private readonly ISendGridClient? _client;

    public SendGridEmailService(
        IOptions<SendGridOptions> options,
        ILogger<SendGridEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _client = new SendGridClient(_options.ApiKey);
        }
    }

    public async Task<bool> SendEmailAsync(
        string to,
        string subject,
        string htmlContent,
        string? plainTextContent = null,
        CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            _logger.LogWarning("SendGrid API key not configured. Email not sent to {To}", to);
            return false;
        }

        try
        {
            var from = new EmailAddress(_options.FromEmail, _options.FromName);
            var recipients = to.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(email => new EmailAddress(email.Trim()))
                .ToList();

            if (recipients.Count == 0)
            {
                _logger.LogWarning("No valid recipients provided for email");
                return false;
            }

            var msg = MailHelper.CreateSingleEmailToMultipleRecipients(
                from,
                recipients,
                subject,
                plainTextContent ?? StripHtml(htmlContent),
                htmlContent);

            var response = await _client.SendEmailAsync(msg, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {To}: {Subject}", to, subject);
                return true;
            }

            var body = await response.Body.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Failed to send email. Status: {StatusCode}, Body: {Body}",
                response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {To}", to);
            return false;
        }
    }

    public async Task<bool> SendAlertEmailAsync(
        string to,
        string alertTitle,
        string alertMessage,
        string severity,
        string? mpaName = null,
        CancellationToken cancellationToken = default)
    {
        var severityColor = severity.ToLowerInvariant() switch
        {
            "critical" => "#dc3545",
            "high" => "#fd7e14",
            "medium" => "#ffc107",
            "low" => "#28a745",
            _ => "#6c757d"
        };

        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
</head>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; margin: 0; padding: 20px; background-color: #0a1628;"">
    <div style=""max-width: 600px; margin: 0 auto; background-color: #1a2744; border-radius: 8px; overflow: hidden; border: 1px solid #2a3f5f;"">
        <div style=""background: linear-gradient(135deg, {severityColor}22, {severityColor}44); padding: 20px; border-bottom: 1px solid #2a3f5f;"">
            <h1 style=""color: {severityColor}; margin: 0; font-size: 24px;"">
                ⚠️ CoralLedger Blue Alert
            </h1>
        </div>
        <div style=""padding: 20px;"">
            <div style=""background-color: #0d1929; border-radius: 6px; padding: 16px; margin-bottom: 16px;"">
                <h2 style=""color: #e8f4f8; margin: 0 0 8px 0; font-size: 18px;"">{alertTitle}</h2>
                <p style=""color: #94a3b8; margin: 0; line-height: 1.6;"">{alertMessage}</p>
            </div>
            <table style=""width: 100%; border-collapse: collapse;"">
                <tr>
                    <td style=""padding: 8px 0; color: #64748b; font-size: 14px;"">Severity</td>
                    <td style=""padding: 8px 0; color: {severityColor}; font-weight: 600; text-align: right;"">{severity}</td>
                </tr>
                {(mpaName != null ? $@"
                <tr>
                    <td style=""padding: 8px 0; color: #64748b; font-size: 14px;"">Location</td>
                    <td style=""padding: 8px 0; color: #e8f4f8; text-align: right;"">{mpaName}</td>
                </tr>" : "")}
                <tr>
                    <td style=""padding: 8px 0; color: #64748b; font-size: 14px;"">Time</td>
                    <td style=""padding: 8px 0; color: #e8f4f8; text-align: right;"">{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</td>
                </tr>
            </table>
        </div>
        <div style=""background-color: #0d1929; padding: 16px; text-align: center; border-top: 1px solid #2a3f5f;"">
            <a href=""{_options.DashboardUrl}"" style=""display: inline-block; background: linear-gradient(135deg, #00d4aa, #00b894); color: #0a1628; padding: 12px 24px; border-radius: 6px; text-decoration: none; font-weight: 600;"">
                View Dashboard
            </a>
        </div>
        <div style=""padding: 16px; text-align: center;"">
            <p style=""color: #64748b; font-size: 12px; margin: 0;"">
                CoralLedger Blue - Marine Intelligence Platform for The Bahamas
            </p>
        </div>
    </div>
</body>
</html>";

        var plainText = $@"
CoralLedger Blue Alert

{alertTitle}

{alertMessage}

Severity: {severity}
{(mpaName != null ? $"Location: {mpaName}\n" : "")}Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC

View Dashboard: {_options.DashboardUrl}

--
CoralLedger Blue - Marine Intelligence Platform for The Bahamas
";

        return await SendEmailAsync(to, $"[{severity}] {alertTitle}", htmlContent, plainText, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SendEmailWithAttachmentAsync(
        string to,
        string subject,
        string htmlContent,
        byte[] attachmentContent,
        string attachmentFileName,
        string attachmentContentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            _logger.LogWarning("SendGrid API key not configured. Email with attachment not sent to {To}", to);
            return false;
        }

        try
        {
            var from = new EmailAddress(_options.FromEmail, _options.FromName);
            var toAddress = new EmailAddress(to.Trim());

            var msg = MailHelper.CreateSingleEmail(
                from,
                toAddress,
                subject,
                StripHtml(htmlContent),
                htmlContent);

            // Add attachment
            var base64Content = Convert.ToBase64String(attachmentContent);
            msg.AddAttachment(attachmentFileName, base64Content, attachmentContentType);

            var response = await _client.SendEmailAsync(msg, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email with attachment sent successfully to {To}: {Subject}, Attachment: {FileName}",
                    to, subject, attachmentFileName);
                return true;
            }

            var body = await response.Body.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Failed to send email with attachment. Status: {StatusCode}, Body: {Body}",
                response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email with attachment to {To}", to);
            return false;
        }
    }

    private static string StripHtml(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " ")
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Trim();
    }
}

/// <summary>
/// Configuration options for SendGrid email service
/// </summary>
public class SendGridOptions
{
    public const string SectionName = "SendGrid";

    /// <summary>
    /// SendGrid API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// From email address
    /// </summary>
    public string FromEmail { get; set; } = "alerts@coralledgerblue.com";

    /// <summary>
    /// From name
    /// </summary>
    public string FromName { get; set; } = "CoralLedger Blue";

    /// <summary>
    /// Dashboard URL for email links
    /// </summary>
    public string DashboardUrl { get; set; } = "https://coralledgerblue.com/dashboard";
}
