using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CourseInventory.Web.Models;
using CourseInventory.Web.ViewModels;
using Microsoft.Extensions.Options;

namespace CourseInventory.Web.Services;

public interface ISalesforceService
{
    Task<SalesforceSendResult> SendProfileAsync(
        ApplicationUser user,
        SalesforceProfileFormViewModel form,
        CancellationToken cancellationToken = default);
}

public record SalesforceSendResult(bool Success, string? AccountId = null, string? ContactId = null, string? Error = null)
{
    public static SalesforceSendResult Fail(string error) => new(false, Error: error);
}

public class SalesforceService(
    HttpClient http,
    IOptions<SalesforceOptions> options,
    ILogger<SalesforceService> logger) : ISalesforceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SalesforceOptions salesforce = options.Value;

    public async Task<SalesforceSendResult> SendProfileAsync(
        ApplicationUser user,
        SalesforceProfileFormViewModel form,
        CancellationToken cancellationToken = default)
    {
        if (!salesforce.IsConfigured)
        {
            return SalesforceSendResult.Fail("Salesforce is not configured. Ask an administrator to set the Salesforce secrets.");
        }

        try
        {
            var auth = await AuthenticateAsync(cancellationToken);
            if (auth is null)
            {
                return SalesforceSendResult.Fail("Salesforce authentication failed.");
            }

            var accountId = await CreateAccountAsync(auth, form, cancellationToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return SalesforceSendResult.Fail("Salesforce Account could not be created.");
            }

            var contactId = await CreateContactAsync(auth, accountId, user, form, cancellationToken);
            if (string.IsNullOrWhiteSpace(contactId))
            {
                return SalesforceSendResult.Fail("Salesforce Contact could not be created.");
            }

            return new SalesforceSendResult(true, accountId, contactId);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Salesforce HTTP request failed");
            return SalesforceSendResult.Fail("Salesforce request failed. Check credentials, network access and object permissions.");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Salesforce returned an unexpected response");
            return SalesforceSendResult.Fail("Salesforce returned an unexpected response.");
        }
    }

    private async Task<SalesforceAuthResponse?> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var values = salesforce.UsesClientCredentials
            ? new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = salesforce.ClientId,
                ["client_secret"] = salesforce.ClientSecret
            }
            : new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = salesforce.ClientId,
                ["client_secret"] = salesforce.ClientSecret,
                ["username"] = salesforce.Username,
                ["password"] = salesforce.Password + salesforce.SecurityToken
            };

        using var content = new FormUrlEncodedContent(values);

        using var response = await http.PostAsync(
            BuildUrl(salesforce.LoginUrl, "/services/oauth2/token"),
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Salesforce authentication failed for {AuthFlow} flow with status {StatusCode}: {Response}",
                salesforce.AuthFlow,
                response.StatusCode,
                responseText);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<SalesforceAuthResponse>(stream, JsonOptions, cancellationToken);
    }

    private async Task<string?> CreateAccountAsync(
        SalesforceAuthResponse auth,
        SalesforceProfileFormViewModel form,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["Name"] = form.CompanyName,
            ["Phone"] = form.Phone,
            ["BillingCity"] = form.City,
            ["BillingCountry"] = form.Country,
            ["Description"] = form.Notes
        };

        return await CreateSObjectAsync(auth, "Account", payload, cancellationToken);
    }

    private async Task<string?> CreateContactAsync(
        SalesforceAuthResponse auth,
        string accountId,
        ApplicationUser user,
        SalesforceProfileFormViewModel form,
        CancellationToken cancellationToken)
    {
        var displayName = user.UserName ?? user.Email ?? "Inventory Studio User";
        var nameParts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstName = nameParts.Length > 1 ? nameParts[0] : null;
        var lastName = nameParts.Length > 1 ? nameParts[1] : displayName;

        var payload = new Dictionary<string, object?>
        {
            ["AccountId"] = accountId,
            ["FirstName"] = firstName,
            ["LastName"] = lastName,
            ["Email"] = user.Email,
            ["Phone"] = form.Phone,
            ["Title"] = form.JobTitle,
            ["MailingCity"] = form.City,
            ["MailingCountry"] = form.Country,
            ["Description"] = form.Notes
        };

        return await CreateSObjectAsync(auth, "Contact", payload, cancellationToken);
    }

    private async Task<string?> CreateSObjectAsync(
        SalesforceAuthResponse auth,
        string objectName,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUrl(auth.InstanceUrl, $"/services/data/v{NormalizeApiVersion(salesforce.ApiVersion)}/sobjects/{objectName}"))
        {
            Content = JsonContent.Create(
                payload.Where(pair => pair.Value is not null && !string.IsNullOrWhiteSpace(pair.Value.ToString()))
                    .ToDictionary(pair => pair.Key, pair => pair.Value),
                options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Salesforce {ObjectName} creation failed with status {StatusCode}", objectName, response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<SalesforceCreateResponse>(stream, JsonOptions, cancellationToken);
        return result?.Success == true ? result.Id : null;
    }

    private static string BuildUrl(string baseUrl, string path) =>
        new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path.TrimStart('/')).ToString();

    private static string NormalizeApiVersion(string version) =>
        string.IsNullOrWhiteSpace(version) ? "60.0" : version.Trim().TrimStart('v', 'V');

    private sealed class SalesforceAuthResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("instance_url")]
        public string InstanceUrl { get; set; } = string.Empty;
    }

    private sealed class SalesforceCreateResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}
