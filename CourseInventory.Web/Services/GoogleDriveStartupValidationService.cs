using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace CourseInventory.Web.Services;

public class GoogleDriveStartupValidationService(
    IOptions<GoogleDriveOptions> options,
    ILogger<GoogleDriveStartupValidationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var googleDrive = options.Value;
        var result = GoogleDriveConfigurationValidator.ValidateGoogleDriveConfiguration(googleDrive, logger);

        if (result.IsValid)
        {
            logger.LogInformation(
                "Google Drive support ticket configuration is valid. ClientEmail={ClientEmail}, FolderId={FolderId}, UsedBase64={UsedBase64}",
                result.ClientEmail,
                googleDrive.SupportTicketsFolderId,
                result.UsedBase64);

            await VerifyFolderExistsAsync(googleDrive, result.CredentialJson!, cancellationToken);
        }
        else
        {
            logger.LogWarning(
                "Google Drive support ticket configuration is invalid: {ErrorMessage}",
                result.ErrorMessage);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task VerifyFolderExistsAsync(
        GoogleDriveOptions googleDrive,
        string credentialJson,
        CancellationToken cancellationToken)
    {
        try
        {
            var credential = GoogleCredential
                .FromJson(credentialJson)
                .CreateScoped(DriveService.Scope.Drive);

            using var drive = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Inventory Studio"
            });

            var request = drive.Files.Get(googleDrive.SupportTicketsFolderId);
            request.Fields = "id,name,mimeType";
            request.SupportsAllDrives = true;

            var folder = await request.ExecuteAsync(cancellationToken);
            if (!string.Equals(folder.MimeType, "application/vnd.google-apps.folder", StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Google Drive configured FolderId exists but is not a folder. FolderId={FolderId}, MimeType={MimeType}",
                    googleDrive.SupportTicketsFolderId,
                    folder.MimeType);
                return;
            }

            logger.LogInformation(
                "Google Drive support ticket folder verified. FolderId={FolderId}, FolderName={FolderName}",
                folder.Id,
                folder.Name);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning(
                "Google Drive folder not found. FolderId={FolderId}, GoogleError={GoogleError}",
                googleDrive.SupportTicketsFolderId,
                ex.Error?.Message);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            logger.LogWarning(
                "Google Drive access denied. FolderId={FolderId}, GoogleError={GoogleError}",
                googleDrive.SupportTicketsFolderId,
                ex.Error?.Message);
        }
        catch (GoogleApiException ex)
        {
            logger.LogWarning(
                "Google Drive startup folder verification failed. StatusCode={StatusCode}, FolderId={FolderId}, GoogleError={GoogleError}",
                ex.HttpStatusCode,
                googleDrive.SupportTicketsFolderId,
                ex.Error?.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Google Drive startup folder verification failed unexpectedly. FolderId={FolderId}",
                googleDrive.SupportTicketsFolderId);
        }
    }
}
