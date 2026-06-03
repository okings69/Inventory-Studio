namespace CourseInventory.Web.Services;

public class GoogleDriveOptions
{
    public string ServiceAccountJson { get; set; } = string.Empty;
    public string ServiceAccountJsonBase64 { get; set; } = string.Empty;
    public string SupportTicketsFolderId { get; set; } = string.Empty;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(ServiceAccountJson) ||
        !string.IsNullOrWhiteSpace(ServiceAccountJsonBase64);

    public bool IsConfigured =>
        HasCredentials &&
        !string.IsNullOrWhiteSpace(SupportTicketsFolderId);
}
