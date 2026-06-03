using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CourseInventory.Web.Models;
using CourseInventory.Web.ViewModels;
using Microsoft.Extensions.Options;

namespace CourseInventory.Web.Services;

public interface IHubSpotService
{
    Task<HubSpotProfileResult> SendProfileAsync(
        ApplicationUser user,
        HubSpotProfileFormViewModel form,
        CancellationToken cancellationToken = default);
}

public record HubSpotProfileResult(
    bool Success,
    string? CompanyId = null,
    string? ContactId = null,
    bool AssociationCreated = false,
    string? Error = null)
{
    public static HubSpotProfileResult Fail(string error) => new(false, Error: error);
}

public class HubSpotService(
    HttpClient httpClient,
    IOptions<HubSpotOptions> options,
    ILogger<HubSpotService> logger) : IHubSpotService
{
    private const string HubSpotBaseUrl = "https://api.hubapi.com";

    public async Task<HubSpotProfileResult> SendProfileAsync(
        ApplicationUser user,
        HubSpotProfileFormViewModel form,
        CancellationToken cancellationToken = default)
    {
        var accessToken = options.Value.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return HubSpotProfileResult.Fail("HubSpot is not configured");
        }

        httpClient.BaseAddress ??= new Uri(HubSpotBaseUrl);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var companyId = await CreateCompanyAsync(form, cancellationToken);
        if (companyId.Error is not null)
        {
            return HubSpotProfileResult.Fail(companyId.Error);
        }

        var contactId = await CreateContactAsync(user, form, cancellationToken);
        if (contactId.Error is not null)
        {
            return HubSpotProfileResult.Fail(contactId.Error);
        }

        var associationCreated = await TryAssociateContactWithCompanyAsync(
            contactId.Value!,
            companyId.Value!,
            cancellationToken);

        return new HubSpotProfileResult(
            true,
            CompanyId: companyId.Value,
            ContactId: contactId.Value,
            AssociationCreated: associationCreated);
    }

    private async Task<HubSpotObjectResult> CreateCompanyAsync(
        HubSpotProfileFormViewModel form,
        CancellationToken cancellationToken)
    {
        var properties = RemoveEmptyValues(new Dictionary<string, string?>
        {
            ["name"] = form.CompanyName,
            ["phone"] = form.Phone,
            ["city"] = form.City,
            ["country"] = form.Country,
            ["description"] = form.Notes
        });

        return await CreateObjectAsync(
            "/crm/v3/objects/companies",
            properties,
            "Company could not be created",
            cancellationToken);
    }

    private async Task<HubSpotObjectResult> CreateContactAsync(
        ApplicationUser user,
        HubSpotProfileFormViewModel form,
        CancellationToken cancellationToken)
    {
        var (firstName, lastName) = SplitName(form.UserName);
        var properties = RemoveEmptyValues(new Dictionary<string, string?>
        {
            ["email"] = form.UserEmail ?? user.Email,
            ["firstname"] = firstName,
            ["lastname"] = lastName,
            ["phone"] = form.Phone ?? user.PhoneNumber,
            ["jobtitle"] = form.JobTitle,
            ["city"] = form.City,
            ["country"] = form.Country,
            ["company"] = form.CompanyName
        });

        return await CreateObjectAsync(
            "/crm/v3/objects/contacts",
            properties,
            "Contact could not be created",
            cancellationToken);
    }

    private async Task<HubSpotObjectResult> CreateObjectAsync(
        string path,
        IReadOnlyDictionary<string, string> properties,
        string genericError,
        CancellationToken cancellationToken)
    {
        using var content = JsonContent(new { properties });
        using var response = await httpClient.PostAsync(path, content, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogWarning("HubSpot authentication failed with status {StatusCode} on {Path}", response.StatusCode, path);
            return HubSpotObjectResult.Fail("HubSpot authentication failed");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await SafeReadBodyAsync(response, cancellationToken);
            logger.LogWarning(
                "HubSpot object creation failed with status {StatusCode} on {Path}. Response: {Response}",
                response.StatusCode,
                path,
                errorBody);
            return HubSpotObjectResult.Fail(genericError);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.TryGetProperty("id", out var idElement))
        {
            var id = idElement.GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return HubSpotObjectResult.Ok(id);
            }
        }

        logger.LogWarning("HubSpot object creation succeeded but no id was returned for {Path}", path);
        return HubSpotObjectResult.Fail(genericError);
    }

    private async Task<bool> TryAssociateContactWithCompanyAsync(
        string contactId,
        string companyId,
        CancellationToken cancellationToken)
    {
        var path = $"/crm/v4/objects/contact/{Uri.EscapeDataString(contactId)}/associations/default/company/{Uri.EscapeDataString(companyId)}";
        using var response = await httpClient.PutAsync(path, content: null, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var errorBody = await SafeReadBodyAsync(response, cancellationToken);
        logger.LogWarning(
            "HubSpot contact-company association failed with status {StatusCode}. Response: {Response}",
            response.StatusCode,
            errorBody);
        return false;
    }

    private static StringContent JsonContent<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static Dictionary<string, string> RemoveEmptyValues(Dictionary<string, string?> values) =>
        values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value!.Trim());

    private static (string? FirstName, string? LastName) SplitName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        var parts = value.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 1 ? (parts[0], null) : (parts[0], parts[1]);
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return "(response body unavailable)";
        }
    }

    private record HubSpotObjectResult(bool Success, string? Value = null, string? Error = null)
    {
        public static HubSpotObjectResult Ok(string value) => new(true, value);
        public static HubSpotObjectResult Fail(string error) => new(false, Error: error);
    }
}
