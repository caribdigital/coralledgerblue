namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Service for sending emails
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send an email to one or more recipients
    /// </summary>
    /// <param name="to">Comma-separated list of email addresses</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlContent">HTML content of the email</param>
    /// <param name="plainTextContent">Plain text fallback content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendEmailAsync(
        string to,
        string subject,
        string htmlContent,
        string? plainTextContent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send an alert notification email
    /// </summary>
    Task<bool> SendAlertEmailAsync(
        string to,
        string alertTitle,
        string alertMessage,
        string severity,
        string? mpaName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send an email with a file attachment
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlContent">HTML content of the email</param>
    /// <param name="attachmentContent">File content as byte array</param>
    /// <param name="attachmentFileName">File name for the attachment</param>
    /// <param name="attachmentContentType">MIME type of the attachment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendEmailWithAttachmentAsync(
        string to,
        string subject,
        string htmlContent,
        byte[] attachmentContent,
        string attachmentFileName,
        string attachmentContentType = "application/octet-stream",
        CancellationToken cancellationToken = default);
}
