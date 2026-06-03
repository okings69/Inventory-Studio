using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

namespace CourseInventory.Web.Services;

public record GoogleDriveConfigurationValidationResult(
    bool IsValid,
    string? CredentialJson,
    string? ErrorMessage,
    string? CredentialType,
    string? ClientEmail,
    bool UsedBase64);

public static class GoogleDriveConfigurationValidator
{
    public const string InvalidBase64Message = "Google Drive Base64 credential is invalid.";
    public const string InvalidJsonMessage = "Google Drive JSON credential is invalid.";
    public const string WrongCredentialFileMessage = "Wrong Google Drive credential file: this is a support ticket JSON, not a service account key.";
    public const string MissingCredentialTypeMessage = "Google Drive credential type is missing.";
    public const string ServiceAccountRequiredMessage = "Google Drive service account credential is required.";
    public const string NotConfiguredMessage = "Support ticket upload is not configured yet.";

    public static GoogleDriveConfigurationValidationResult ValidateGoogleDriveConfiguration(
        GoogleDriveOptions googleDrive,
        ILogger logger)
    {
        var hasFolderId = !string.IsNullOrWhiteSpace(googleDrive.SupportTicketsFolderId);
        logger.LogInformation(
            "Google Drive support ticket folder configured: {HasFolderId}. FolderId={FolderId}",
            hasFolderId,
            hasFolderId ? googleDrive.SupportTicketsFolderId : "(missing)");

        if (!hasFolderId)
        {
            return Invalid(NotConfiguredMessage);
        }

        var (credentialJson, usedBase64, credentialError) = ReadCredentialJson(googleDrive, logger);
        if (credentialError is not null)
        {
            return credentialError;
        }

        using var jsonDocument = ParseCredentialJson(credentialJson!, logger, out var parseError);
        if (parseError is not null)
        {
            return parseError;
        }

        var root = jsonDocument!.RootElement;
        if (LooksLikeSupportTicketPayload(root))
        {
            logger.LogWarning("Google Drive credential JSON looks like a support ticket payload, not a service account key.");
            return Invalid(WrongCredentialFileMessage, credentialJson, usedBase64: usedBase64);
        }

        var credentialType = ReadStringProperty(root, "type");
        var clientEmail = ReadStringProperty(root, "client_email");

        logger.LogInformation(
            "Google Drive credential type detected: {CredentialType}. ClientEmail={ClientEmail}. FolderId={FolderId}",
            string.IsNullOrWhiteSpace(credentialType) ? "(missing)" : credentialType,
            string.IsNullOrWhiteSpace(clientEmail) ? "(missing)" : clientEmail,
            googleDrive.SupportTicketsFolderId);

        if (string.IsNullOrWhiteSpace(credentialType))
        {
            return Invalid(MissingCredentialTypeMessage, credentialJson, credentialType, clientEmail, usedBase64);
        }

        if (!string.Equals(credentialType, "service_account", StringComparison.Ordinal))
        {
            return Invalid(ServiceAccountRequiredMessage, credentialJson, credentialType, clientEmail, usedBase64);
        }

        try
        {
            _ = GoogleCredential.FromJson(credentialJson);
            logger.LogInformation(
                "GoogleCredential.FromJson accepted Google Drive service account credentials for {ClientEmail}.",
                clientEmail);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            logger.LogWarning(
                "GoogleCredential.FromJson rejected Google Drive credentials. ExceptionType={ExceptionType}, Message={Message}",
                ex.GetType().Name,
                ex.Message);
            return Invalid(InvalidJsonMessage, credentialJson, credentialType, clientEmail, usedBase64);
        }

        return new GoogleDriveConfigurationValidationResult(
            IsValid: true,
            CredentialJson: credentialJson,
            ErrorMessage: null,
            CredentialType: credentialType,
            ClientEmail: clientEmail,
            UsedBase64: usedBase64);
    }

    private static (string? CredentialJson, bool UsedBase64, GoogleDriveConfigurationValidationResult? Error) ReadCredentialJson(
        GoogleDriveOptions googleDrive,
        ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(googleDrive.ServiceAccountJson))
        {
            logger.LogInformation("Google Drive raw service account JSON is configured.");
            return (googleDrive.ServiceAccountJson, false, null);
        }

        if (string.IsNullOrWhiteSpace(googleDrive.ServiceAccountJsonBase64))
        {
            logger.LogWarning("Google Drive service account credentials are missing.");
            return (null, false, Invalid(NotConfiguredMessage));
        }

        try
        {
            var bytes = Convert.FromBase64String(googleDrive.ServiceAccountJsonBase64);
            var json = Encoding.UTF8.GetString(bytes);
            logger.LogInformation("Google Drive Base64 credential decoded successfully.");
            return (json, true, null);
        }
        catch (FormatException)
        {
            logger.LogWarning("Google Drive Base64 credential could not be decoded.");
            return (null, true, Invalid(InvalidBase64Message, usedBase64: true));
        }
    }

    private static JsonDocument? ParseCredentialJson(
        string credentialJson,
        ILogger logger,
        out GoogleDriveConfigurationValidationResult? error)
    {
        try
        {
            var jsonDocument = JsonDocument.Parse(credentialJson);
            logger.LogInformation("Google Drive credential JSON parsed successfully.");
            error = null;
            return jsonDocument;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                "Google Drive credential JSON could not be parsed. ExceptionType={ExceptionType}, Message={Message}",
                ex.GetType().Name,
                ex.Message);
            error = Invalid(InvalidJsonMessage);
            return null;
        }
    }

    private static string? ReadStringProperty(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(propertyName, out var element) &&
        element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static bool LooksLikeSupportTicketPayload(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("reportedBy", out _) &&
        root.TryGetProperty("summary", out _);

    private static GoogleDriveConfigurationValidationResult Invalid(
        string message,
        string? credentialJson = null,
        string? credentialType = null,
        string? clientEmail = null,
        bool usedBase64 = false) =>
        new(
            IsValid: false,
            CredentialJson: credentialJson,
            ErrorMessage: message,
            CredentialType: credentialType,
            ClientEmail: clientEmail,
            UsedBase64: usedBase64);
}
