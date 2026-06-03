using System.Text;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Upload;
using CourseInventory.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CourseInventory.Web.Services;

public interface IGoogleDriveTicketUploadService
{
    Task<string> UploadSupportTicketAsync(
        string fileName,
        string jsonContent,
        CancellationToken cancellationToken = default);
}

public record GoogleOAuthTokenSet(
    string UserId,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt);

public interface IGoogleOAuthTokenProvider
{
    Task<GoogleOAuthTokenSet?> GetCurrentUserTokensAsync(CancellationToken cancellationToken = default);
}

public interface IGoogleDriveFileUploader
{
    Task<string> UploadJsonAsync(
        string fileName,
        string jsonContent,
        string folderId,
        GoogleOAuthTokenSet tokens,
        CancellationToken cancellationToken = default);
}

public class GoogleDriveTicketUploadService(
    IOptions<GoogleDriveOptions> options,
    IGoogleOAuthTokenProvider tokenProvider,
    IGoogleDriveFileUploader fileUploader,
    ILogger<GoogleDriveTicketUploadService> logger) : IGoogleDriveTicketUploadService
{
    public const string MissingOAuthTokenMessage = "Missing Google Drive OAuth token.";
    public const string ConsentRequiredMessage = "Google Drive consent required.";
    public const string FolderNotFoundMessage = "Google Drive folder not found.";
    public const string AccessDeniedMessage = "Google Drive access denied.";

    private readonly GoogleDriveOptions googleDrive = options.Value;

    public async Task<string> UploadSupportTicketAsync(
        string fileName,
        string jsonContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(googleDrive.SupportTicketsFolderId))
        {
            logger.LogWarning("Google Drive support ticket folder ID is missing.");
            throw new SupportTicketException(GoogleDriveConfigurationValidator.NotConfiguredMessage);
        }

        var tokens = await tokenProvider.GetCurrentUserTokensAsync(cancellationToken);
        if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
        {
            logger.LogWarning("Google Drive OAuth access token is missing for the current user.");
            throw new SupportTicketException(MissingOAuthTokenMessage);
        }

        if (tokens.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1) &&
            string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            logger.LogWarning("Google Drive OAuth token is expired and no refresh token is available.");
            throw new SupportTicketException(ConsentRequiredMessage);
        }

        try
        {
            var fileId = await fileUploader.UploadJsonAsync(
                fileName,
                jsonContent,
                googleDrive.SupportTicketsFolderId,
                tokens,
                cancellationToken);

            logger.LogInformation("Uploaded support ticket {FileName} to Google Drive as file {FileId}", fileName, fileId);
            return fileId;
        }
        catch (SupportTicketException)
        {
            throw;
        }
        catch (GoogleApiException ex)
        {
            logger.LogWarning(
                ex,
                "Google Drive support ticket upload failed with status {StatusCode} and Google error {Error}",
                ex.HttpStatusCode,
                ex.Error?.Message);
            throw new SupportTicketException(BuildFriendlyUploadError(ex));
        }
        catch (TokenResponseException ex)
        {
            logger.LogWarning(
                "Google Drive OAuth token refresh failed. Error={Error}, Description={Description}",
                ex.Error?.Error,
                ex.Error?.ErrorDescription);
            throw new SupportTicketException(ConsentRequiredMessage);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google Drive support ticket upload failed");
            throw new SupportTicketException("Support ticket could not be uploaded to Google Drive.");
        }
    }

    private static string BuildFriendlyUploadError(GoogleApiException exception) =>
        exception.HttpStatusCode switch
        {
            System.Net.HttpStatusCode.NotFound => FolderNotFoundMessage,
            System.Net.HttpStatusCode.Forbidden => AccessDeniedMessage,
            System.Net.HttpStatusCode.Unauthorized => ConsentRequiredMessage,
            _ => "Support ticket could not be uploaded to Google Drive."
        };
}

public class GoogleOAuthTokenProvider(
    IHttpContextAccessor httpContextAccessor,
    UserManager<ApplicationUser> users,
    ILogger<GoogleOAuthTokenProvider> logger) : IGoogleOAuthTokenProvider
{
    private const string GoogleProvider = "Google";

    public async Task<GoogleOAuthTokenSet?> GetCurrentUserTokensAsync(CancellationToken cancellationToken = default)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return null;
        }

        var accessToken = await users.GetAuthenticationTokenAsync(user, GoogleProvider, "access_token");
        var refreshToken = await users.GetAuthenticationTokenAsync(user, GoogleProvider, "refresh_token");
        var expiresAtValue = await users.GetAuthenticationTokenAsync(user, GoogleProvider, "expires_at");
        var expiresAt = ParseExpiresAt(expiresAtValue);

        logger.LogInformation(
            "Loaded Google OAuth tokens for user {UserId}. HasAccessToken={HasAccessToken}, HasRefreshToken={HasRefreshToken}, ExpiresAt={ExpiresAt}",
            user.Id,
            !string.IsNullOrWhiteSpace(accessToken),
            !string.IsNullOrWhiteSpace(refreshToken),
            expiresAt);

        return new GoogleOAuthTokenSet(user.Id, accessToken, refreshToken, expiresAt);
    }

    private static DateTimeOffset? ParseExpiresAt(string? value) =>
        DateTimeOffset.TryParse(value, out var expiresAt)
            ? expiresAt
            : null;
}

public class GoogleDriveFileUploader(
    IConfiguration configuration,
    ILogger<GoogleDriveFileUploader> logger) : IGoogleDriveFileUploader
{
    public async Task<string> UploadJsonAsync(
        string fileName,
        string jsonContent,
        string folderId,
        GoogleOAuthTokenSet tokens,
        CancellationToken cancellationToken = default)
    {
        var credential = await CreateCredentialAsync(tokens, cancellationToken);

        using var drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Inventory Studio"
        });

        var metadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileName,
            MimeType = "application/json",
            Parents = [folderId]
        };

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
        var request = drive.Files.Create(metadata, stream, "application/json");
        request.Fields = "id";
        request.SupportsAllDrives = true;

        var upload = await request.UploadAsync(cancellationToken);
        if (upload.Status != UploadStatus.Completed || string.IsNullOrWhiteSpace(request.ResponseBody?.Id))
        {
            logger.LogWarning(
                "Google Drive upload failed with status {Status}, exception {ExceptionType}, message {Message}",
                upload.Status,
                upload.Exception?.GetType().Name,
                upload.Exception?.Message);

            if (upload.Exception is GoogleApiException googleApiException)
            {
                throw googleApiException;
            }

            throw new SupportTicketException("Support ticket could not be uploaded to Google Drive.");
        }

        return request.ResponseBody.Id;
    }

    private async Task<IConfigurableHttpClientInitializer> CreateCredentialAsync(
        GoogleOAuthTokenSet tokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return GoogleCredential.FromAccessToken(tokens.AccessToken).CreateScoped(DriveService.Scope.DriveFile);
        }

        var clientId = configuration["Authentication:Google:ClientId"];
        var clientSecret = configuration["Authentication:Google:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new SupportTicketException(GoogleDriveTicketUploadService.ConsentRequiredMessage);
        }

        var token = new TokenResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            IssuedUtc = DateTime.UtcNow,
            ExpiresInSeconds = BuildExpiresInSeconds(tokens.ExpiresAt)
        };

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            },
            Scopes = [DriveService.Scope.DriveFile]
        });

        var credential = new UserCredential(flow, tokens.UserId, token);
        if (tokens.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            await credential.RefreshTokenAsync(cancellationToken);
        }

        return credential;
    }

    private static long? BuildExpiresInSeconds(DateTimeOffset? expiresAt)
    {
        if (expiresAt is null)
        {
            return null;
        }

        var seconds = (long)Math.Max(0, (expiresAt.Value - DateTimeOffset.UtcNow).TotalSeconds);
        return seconds;
    }
}
