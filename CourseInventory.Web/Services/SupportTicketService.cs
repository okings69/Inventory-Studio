using System.Text.Json;
using System.Text.Json.Serialization;

namespace CourseInventory.Web.Services;

public interface ISupportTicketService
{
    Task SubmitAsync(SupportTicketRequest request, CancellationToken cancellationToken = default);
}

public record SupportTicketRequest(
    string ReportedBy,
    string? ReportedByEmail,
    string Inventory,
    string Link,
    string Priority,
    string Summary);

public record SupportTicketPayload(
    string ReportedBy,
    string? ReportedByEmail,
    string Inventory,
    string Link,
    string Priority,
    string Summary,
    DateTime CreatedAtUtc);

public class SupportTicketException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class SupportTicketService(
    IGoogleDriveTicketUploadService uploadService,
    TimeProvider timeProvider,
    ILogger<SupportTicketService> logger) : ISupportTicketService
{
    public static readonly string[] AllowedPriorities = ["Low", "Average", "High"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public async Task SubmitAsync(SupportTicketRequest request, CancellationToken cancellationToken = default)
    {
        var priority = NormalizePriority(request.Priority);
        if (priority is null)
        {
            throw new SupportTicketException("Choose Low, Average, or High priority.");
        }

        if (string.IsNullOrWhiteSpace(request.ReportedBy))
        {
            throw new SupportTicketException("The reporting user is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Inventory))
        {
            throw new SupportTicketException("The inventory is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            throw new SupportTicketException("Summary is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Link) ||
            !Uri.TryCreate(request.Link, UriKind.Absolute, out var link) ||
            (link.Scheme != Uri.UriSchemeHttp && link.Scheme != Uri.UriSchemeHttps))
        {
            throw new SupportTicketException("The inventory link is invalid.");
        }

        var createdAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var payload = new SupportTicketPayload(
            ReportedBy: request.ReportedBy.Trim(),
            ReportedByEmail: string.IsNullOrWhiteSpace(request.ReportedByEmail) ? null : request.ReportedByEmail.Trim(),
            Inventory: request.Inventory.Trim(),
            Link: link.ToString(),
            Priority: priority,
            Summary: request.Summary.Trim(),
            CreatedAtUtc: createdAtUtc);

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var fileName = BuildFileName(createdAtUtc);
        var fileId = await uploadService.UploadSupportTicketAsync(fileName, json, cancellationToken);
        logger.LogInformation("Support ticket uploaded as Google Drive file {FileId}", fileId);
    }

    public static string BuildFileName(DateTime createdAtUtc) =>
        $"support-ticket-{createdAtUtc:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.json";

    private static string? NormalizePriority(string? priority) =>
        AllowedPriorities.FirstOrDefault(allowed =>
            string.Equals(allowed, priority?.Trim(), StringComparison.OrdinalIgnoreCase));
}
