using System.Text;
using System.Text.Json;
using CourseInventory.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CourseInventory.Web.Tests;

public class SupportTicketServiceTests
{
    [Fact]
    public async Task SubmitAsync_BuildsPayloadAndUploadsJson()
    {
        var upload = new CapturingGoogleDriveUploadService();
        var service = CreateService(upload);

        await service.SubmitAsync(new SupportTicketRequest(
            ReportedBy: "Orian",
            ReportedByEmail: "oriangidolcalebou@gmail.com",
            Inventory: "Lab Equipment",
            Link: "https://inventory-studio-web.onrender.com/Inventories/Details/42",
            Priority: "High",
            Summary: "The export page fails when I click Download."));

        Assert.StartsWith("support-ticket-20260531-180000-", upload.FileName);
        Assert.EndsWith(".json", upload.FileName);
        Assert.Equal("application/json", upload.ContentTypeHint);

        using var json = JsonDocument.Parse(upload.JsonContent);
        Assert.Equal("Orian", json.RootElement.GetProperty("reportedBy").GetString());
        Assert.Equal("oriangidolcalebou@gmail.com", json.RootElement.GetProperty("reportedByEmail").GetString());
        Assert.Equal("Lab Equipment", json.RootElement.GetProperty("inventory").GetString());
        Assert.Equal("High", json.RootElement.GetProperty("priority").GetString());
        Assert.Equal("The export page fails when I click Download.", json.RootElement.GetProperty("summary").GetString());
        Assert.Equal("2026-05-31T18:00:00Z", json.RootElement.GetProperty("createdAtUtc").GetDateTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }

    [Fact]
    public void BuildFileName_UsesRequiredFormat()
    {
        var fileName = SupportTicketService.BuildFileName(new DateTime(2026, 5, 31, 18, 0, 0, DateTimeKind.Utc));

        Assert.Matches(@"^support-ticket-20260531-180000-[a-f0-9]{8}\.json$", fileName);
    }

    [Fact]
    public async Task SubmitAsync_RejectsInvalidPriority()
    {
        var upload = new CapturingGoogleDriveUploadService();
        var service = CreateService(upload);

        var ex = await Assert.ThrowsAsync<SupportTicketException>(() =>
            service.SubmitAsync(new SupportTicketRequest(
                ReportedBy: "Orian",
                ReportedByEmail: "oriangidolcalebou@gmail.com",
                Inventory: "Lab Equipment",
                Link: "https://inventory.example.com/Inventories/Details/42",
                Priority: "Urgent",
                Summary: "Something broke.")));

        Assert.Contains("Low, Average, or High", ex.Message);
        Assert.False(upload.WasCalled);
    }

    [Fact]
    public async Task GoogleDriveUploadService_WhenMissingConfiguration_ReturnsFriendlyException()
    {
        var service = new GoogleDriveTicketUploadService(
            Options.Create(new GoogleDriveOptions()),
            new StubGoogleOAuthTokenProvider(new GoogleOAuthTokenSet("user-id", "access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1))),
            new CapturingGoogleDriveFileUploader(),
            NullLogger<GoogleDriveTicketUploadService>.Instance);

        var ex = await Assert.ThrowsAsync<SupportTicketException>(() =>
            service.UploadSupportTicketAsync("ticket.json", "{}"));

        Assert.Contains("not configured", ex.Message);
    }

    [Fact]
    public async Task GoogleDriveUploadService_WhenOAuthTokenMissing_ReturnsFriendlyException()
    {
        var service = new GoogleDriveTicketUploadService(
            Options.Create(new GoogleDriveOptions { SupportTicketsFolderId = "folder-id" }),
            new StubGoogleOAuthTokenProvider(null),
            new CapturingGoogleDriveFileUploader(),
            NullLogger<GoogleDriveTicketUploadService>.Instance);

        var ex = await Assert.ThrowsAsync<SupportTicketException>(() =>
            service.UploadSupportTicketAsync("ticket.json", "{}"));

        Assert.Equal(GoogleDriveTicketUploadService.MissingOAuthTokenMessage, ex.Message);
    }

    [Fact]
    public async Task GoogleDriveUploadService_UploadsWithUserOAuthToken()
    {
        var uploader = new CapturingGoogleDriveFileUploader();
        var service = new GoogleDriveTicketUploadService(
            Options.Create(new GoogleDriveOptions { SupportTicketsFolderId = "folder-id" }),
            new StubGoogleOAuthTokenProvider(new GoogleOAuthTokenSet("user-id", "access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1))),
            uploader,
            NullLogger<GoogleDriveTicketUploadService>.Instance);

        var fileId = await service.UploadSupportTicketAsync("ticket.json", "{\"ok\":true}");

        Assert.Equal("google-file-id", fileId);
        Assert.Equal("ticket.json", uploader.FileName);
        Assert.Equal("{\"ok\":true}", uploader.JsonContent);
        Assert.Equal("folder-id", uploader.FolderId);
        Assert.Equal("access-token", uploader.Tokens.AccessToken);
    }

    [Fact]
    public void ValidateGoogleDriveConfiguration_RejectsInvalidBase64()
    {
        var result = GoogleDriveConfigurationValidator.ValidateGoogleDriveConfiguration(
            new GoogleDriveOptions
            {
                ServiceAccountJsonBase64 = "not base64",
                SupportTicketsFolderId = "folder-id"
            },
            NullLogger.Instance);

        Assert.False(result.IsValid);
        Assert.Equal(GoogleDriveConfigurationValidator.InvalidBase64Message, result.ErrorMessage);
    }

    [Fact]
    public void ValidateGoogleDriveConfiguration_RejectsInvalidJson()
    {
        var result = GoogleDriveConfigurationValidator.ValidateGoogleDriveConfiguration(
            new GoogleDriveOptions
            {
                ServiceAccountJsonBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("{ invalid json")),
                SupportTicketsFolderId = "folder-id"
            },
            NullLogger.Instance);

        Assert.False(result.IsValid);
        Assert.Equal(GoogleDriveConfigurationValidator.InvalidJsonMessage, result.ErrorMessage);
    }

    [Fact]
    public void ValidateGoogleDriveConfiguration_RejectsSupportTicketJson()
    {
        var ticketPayloadJson = """
            {
              "reportedBy": "Orian",
              "reportedByEmail": "oriangidolcalebou@gmail.com",
              "inventory": "Inventory Studio",
              "link": "https://inventory-studio-web.onrender.com",
              "priority": "Average",
              "summary": "Second test",
              "createdAtUtc": "2026-05-31T19:00:00Z"
            }
            """;

        var result = GoogleDriveConfigurationValidator.ValidateGoogleDriveConfiguration(
            new GoogleDriveOptions
            {
                ServiceAccountJsonBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(ticketPayloadJson)),
                SupportTicketsFolderId = "folder-id"
            },
            NullLogger.Instance);

        Assert.False(result.IsValid);
        Assert.Equal(GoogleDriveConfigurationValidator.WrongCredentialFileMessage, result.ErrorMessage);
    }

    [Fact]
    public void ValidateGoogleDriveConfiguration_RejectsMissingCredentialType()
    {
        var result = GoogleDriveConfigurationValidator.ValidateGoogleDriveConfiguration(
            new GoogleDriveOptions
            {
                ServiceAccountJson = """
                    {
                      "client_email": "inventory-studio@example.iam.gserviceaccount.com"
                    }
                    """,
                SupportTicketsFolderId = "folder-id"
            },
            NullLogger.Instance);

        Assert.False(result.IsValid);
        Assert.Equal(GoogleDriveConfigurationValidator.MissingCredentialTypeMessage, result.ErrorMessage);
    }

    [Fact]
    public void ValidateGoogleDriveConfiguration_RejectsNonServiceAccountCredentialType()
    {
        var result = GoogleDriveConfigurationValidator.ValidateGoogleDriveConfiguration(
            new GoogleDriveOptions
            {
                ServiceAccountJson = """
                    {
                      "type": "authorized_user",
                      "client_email": "inventory-studio@example.iam.gserviceaccount.com"
                    }
                    """,
                SupportTicketsFolderId = "folder-id"
            },
            NullLogger.Instance);

        Assert.False(result.IsValid);
        Assert.Equal(GoogleDriveConfigurationValidator.ServiceAccountRequiredMessage, result.ErrorMessage);
    }

    private static SupportTicketService CreateService(IGoogleDriveTicketUploadService upload) =>
        new(
            upload,
            new FakeTimeProvider(new DateTimeOffset(2026, 5, 31, 18, 0, 0, TimeSpan.Zero)),
            NullLogger<SupportTicketService>.Instance);

    private sealed class CapturingGoogleDriveUploadService : IGoogleDriveTicketUploadService
    {
        public bool WasCalled { get; private set; }
        public string FileName { get; private set; } = string.Empty;
        public string JsonContent { get; private set; } = string.Empty;
        public string ContentTypeHint { get; private set; } = string.Empty;

        public Task<string> UploadSupportTicketAsync(
            string fileName,
            string jsonContent,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            FileName = fileName;
            JsonContent = jsonContent;
            ContentTypeHint = "application/json";
            return Task.FromResult("google-file-id");
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubGoogleOAuthTokenProvider(GoogleOAuthTokenSet? tokens) : IGoogleOAuthTokenProvider
    {
        public Task<GoogleOAuthTokenSet?> GetCurrentUserTokensAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(tokens);
    }

    private sealed class CapturingGoogleDriveFileUploader : IGoogleDriveFileUploader
    {
        public string FileName { get; private set; } = string.Empty;
        public string JsonContent { get; private set; } = string.Empty;
        public string FolderId { get; private set; } = string.Empty;
        public GoogleOAuthTokenSet Tokens { get; private set; } = new("user-id", null, null, null);

        public Task<string> UploadJsonAsync(
            string fileName,
            string jsonContent,
            string folderId,
            GoogleOAuthTokenSet tokens,
            CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            JsonContent = jsonContent;
            FolderId = folderId;
            Tokens = tokens;
            return Task.FromResult("google-file-id");
        }
    }
}
