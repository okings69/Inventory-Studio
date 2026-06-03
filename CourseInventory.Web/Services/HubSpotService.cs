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
    string? CompanyName = null,
    string? ContactName = null,
    string? Email = null,
    string? HubSpotCompanyUrl = null,
    string? HubSpotContactUrl = null,
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

        var email = form.UserEmail ?? user.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            return HubSpotProfileResult.Fail("HubSpot validation failed: contact email is required.");
        }

        httpClient.BaseAddress ??= new Uri(HubSpotBaseUrl);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        logger.LogInformation(
            "[HubSpot] CompanyName={CompanyName} Email={Email}",
            form.CompanyName,
            email);

        var company = await GetOrCreateCompanyAsync(form, email, cancellationToken);
        if (!company.Success)
        {
            return HubSpotProfileResult.Fail(company.Error!);
        }

        logger.LogInformation("[HubSpot] CompanyId={CompanyId}", company.Id);

        var contact = await GetOrCreateContactAsync(user, form, email, cancellationToken);
        if (!contact.Success)
        {
            return HubSpotProfileResult.Fail(contact.Error!);
        }

        logger.LogInformation("[HubSpot] ContactId={ContactId}", contact.Id);

        var association = await AssociateContactWithCompanyAsync(
            contact.Id!,
            company.Id!,
            cancellationToken);
        if (!association.Success)
        {
            return new HubSpotProfileResult(
                false,
                CompanyId: company.Id,
                ContactId: contact.Id,
                CompanyName: form.CompanyName,
                ContactName: form.UserName,
                Email: email,
                HubSpotCompanyUrl: BuildCompanyUrl(company.Id!),
                HubSpotContactUrl: BuildContactUrl(contact.Id!),
                Error: "Contact was created but could not be associated with the company.");
        }

        return new HubSpotProfileResult(
            true,
            CompanyId: company.Id,
            ContactId: contact.Id,
            AssociationCreated: true,
            CompanyName: form.CompanyName,
            ContactName: form.UserName,
            Email: email,
            HubSpotCompanyUrl: BuildCompanyUrl(company.Id!),
            HubSpotContactUrl: BuildContactUrl(contact.Id!));
    }

    private async Task<HubSpotObjectResult> GetOrCreateCompanyAsync(
        HubSpotProfileFormViewModel form,
        string email,
        CancellationToken cancellationToken)
    {
        var existingCompany = await FindCompanyAsync(form.CompanyName, ExtractDomain(email), cancellationToken);
        if (existingCompany.Success)
        {
            return existingCompany;
        }

        if (existingCompany.IsTerminalError)
        {
            return existingCompany;
        }

        var properties = RemoveEmptyValues(new Dictionary<string, string?>
        {
            ["name"] = form.CompanyName,
            ["domain"] = ExtractDomain(email),
            ["phone"] = form.Phone,
            ["city"] = form.City,
            ["country"] = form.Country,
            ["description"] = form.Notes
        });

        var created = await CreateObjectAsync(
            "CreateCompany",
            "/crm/v3/objects/companies",
            properties,
            "Company could not be created",
            cancellationToken);

        if (created.IsConflict)
        {
            logger.LogInformation("[HubSpot] Record already exists; existing record was reused.");
            var existing = await FindCompanyAsync(form.CompanyName, ExtractDomain(email), cancellationToken);
            return existing.Success
                ? existing
                : HubSpotObjectResult.Fail("Company could not be created", IsTerminalError: true);
        }

        return created;
    }

    private async Task<HubSpotObjectResult> GetOrCreateContactAsync(
        ApplicationUser user,
        HubSpotProfileFormViewModel form,
        string email,
        CancellationToken cancellationToken)
    {
        var existingContact = await SearchSingleObjectAsync(
            "SearchContact",
            "/crm/v3/objects/contacts/search",
            "email",
            email,
            cancellationToken);
        if (existingContact.Success || existingContact.IsTerminalError)
        {
            return existingContact;
        }

        var (firstName, lastName) = SplitName(form.UserName);
        var properties = RemoveEmptyValues(new Dictionary<string, string?>
        {
            ["email"] = email,
            ["firstname"] = firstName,
            ["lastname"] = lastName,
            ["phone"] = form.Phone ?? user.PhoneNumber,
            ["jobtitle"] = form.JobTitle,
            ["city"] = form.City,
            ["country"] = form.Country,
            ["company"] = form.CompanyName
        });

        var created = await CreateObjectAsync(
            "CreateContact",
            "/crm/v3/objects/contacts",
            properties,
            "Contact could not be created",
            cancellationToken);

        if (created.IsConflict)
        {
            logger.LogInformation("[HubSpot] Record already exists; existing record was reused.");
            var existing = await SearchSingleObjectAsync(
                "SearchContact",
                "/crm/v3/objects/contacts/search",
                "email",
                email,
                cancellationToken);
            return existing.Success
                ? existing
                : HubSpotObjectResult.Fail("Contact could not be created", IsTerminalError: true);
        }

        return created;
    }

    private async Task<HubSpotObjectResult> FindCompanyAsync(
        string companyName,
        string? domain,
        CancellationToken cancellationToken)
    {
        var byName = await SearchSingleObjectAsync(
            "SearchCompanyByName",
            "/crm/v3/objects/companies/search",
            "name",
            companyName,
            cancellationToken);
        if (byName.Success || byName.IsTerminalError)
        {
            return byName;
        }

        return !string.IsNullOrWhiteSpace(domain)
            ? await SearchSingleObjectAsync("SearchCompanyByDomain", "/crm/v3/objects/companies/search", "domain", domain, cancellationToken)
            : HubSpotObjectResult.NotFound();
    }

    private async Task<HubSpotObjectResult> SearchSingleObjectAsync(
        string operation,
        string path,
        string propertyName,
        string propertyValue,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            filterGroups = new[]
            {
                new
                {
                    filters = new[]
                    {
                        new
                        {
                            propertyName,
                            @operator = "EQ",
                            value = propertyValue
                        }
                    }
                }
            },
            properties = new[] { propertyName },
            limit = 1
        };

        using var content = JsonContent(payload);
        using var response = await httpClient.PostAsync(path, content, cancellationToken);
        var responseBody = await SafeReadBodyAsync(response, cancellationToken);
        LogHubSpotResponse(operation, response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
        {
            return HubSpotObjectResult.Fail(MapHubSpotError(response.StatusCode, responseBody), IsTerminalError: true);
        }

        var id = TryReadFirstResultId(responseBody);
        return string.IsNullOrWhiteSpace(id) ? HubSpotObjectResult.NotFound() : HubSpotObjectResult.Ok(id);
    }

    private async Task<HubSpotObjectResult> CreateObjectAsync(
        string operation,
        string path,
        IReadOnlyDictionary<string, string> properties,
        string genericError,
        CancellationToken cancellationToken)
    {
        using var content = JsonContent(new { properties });
        using var response = await httpClient.PostAsync(path, content, cancellationToken);
        var responseBody = await SafeReadBodyAsync(response, cancellationToken);
        LogHubSpotResponse(operation, response.StatusCode, responseBody);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return HubSpotObjectResult.Conflict();
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = response.StatusCode == HttpStatusCode.BadRequest
                ? MapHubSpotError(response.StatusCode, responseBody)
                : response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? MapHubSpotError(response.StatusCode, responseBody)
                    : genericError;
            return HubSpotObjectResult.Fail(error, IsTerminalError: true);
        }

        var id = TryReadId(responseBody);
        return string.IsNullOrWhiteSpace(id)
            ? HubSpotObjectResult.Fail(genericError, IsTerminalError: true)
            : HubSpotObjectResult.Ok(id);
    }

    private async Task<HubSpotObjectResult> AssociateContactWithCompanyAsync(
        string contactId,
        string companyId,
        CancellationToken cancellationToken)
    {
        var path = $"/crm/v4/objects/contact/{Uri.EscapeDataString(contactId)}/associations/default/company/{Uri.EscapeDataString(companyId)}";
        using var response = await httpClient.PutAsync(path, content: null, cancellationToken);
        var responseBody = await SafeReadBodyAsync(response, cancellationToken);
        LogHubSpotResponse("AssociateContactCompany", response.StatusCode, responseBody);

        return response.IsSuccessStatusCode
            ? HubSpotObjectResult.Ok("associated")
            : HubSpotObjectResult.Fail("Contact was created but could not be associated with the company.", IsTerminalError: true);
    }

    private void LogHubSpotResponse(string operation, HttpStatusCode statusCode, string responseBody)
    {
        logger.LogInformation("[HubSpot] {Operation} StatusCode={StatusCode}", operation, (int)statusCode);
        logger.LogInformation("[HubSpot] {Operation} Response={Response}", operation, responseBody);
    }

    private static string MapHubSpotError(HttpStatusCode statusCode, string responseBody) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "HubSpot access token is invalid or expired.",
            HttpStatusCode.Forbidden => "HubSpot permissions are insufficient. Check CRM scopes.",
            HttpStatusCode.Conflict => "Record already exists; existing record was reused.",
            HttpStatusCode.BadRequest => $"HubSpot validation failed: {ReadHubSpotMessage(responseBody)}",
            _ => "HubSpot synchronization failed. Check server logs."
        };

    private static string ReadHubSpotMessage(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "Invalid HubSpot request.";
            }
        }
        catch (JsonException)
        {
            return string.IsNullOrWhiteSpace(responseBody) ? "Invalid HubSpot request." : responseBody;
        }

        return "Invalid HubSpot request.";
    }

    private static string? TryReadId(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadFirstResultId(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array ||
                results.GetArrayLength() == 0)
            {
                return null;
            }

            return results[0].TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string? BuildCompanyUrl(string companyId)
    {
        var portalId = options.Value.PortalId;
        return string.IsNullOrWhiteSpace(portalId)
            ? null
            : $"https://app.hubspot.com/contacts/{portalId}/record/0-2/{Uri.EscapeDataString(companyId)}";
    }

    private string? BuildContactUrl(string contactId)
    {
        var portalId = options.Value.PortalId;
        return string.IsNullOrWhiteSpace(portalId)
            ? null
            : $"https://app.hubspot.com/contacts/{portalId}/record/0-1/{Uri.EscapeDataString(contactId)}";
    }

    private static StringContent JsonContent<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
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

    private static string? ExtractDomain(string email)
    {
        var at = email.LastIndexOf('@');
        return at > -1 && at < email.Length - 1 ? email[(at + 1)..].Trim().ToLowerInvariant() : null;
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

    private record HubSpotObjectResult(
        bool Success,
        string? Id = null,
        string? Error = null,
        bool IsConflict = false,
        bool IsTerminalError = false)
    {
        public static HubSpotObjectResult Ok(string id) => new(true, id);
        public static HubSpotObjectResult NotFound() => new(false);
        public static HubSpotObjectResult Conflict() => new(false, IsConflict: true);
        public static HubSpotObjectResult Fail(string error, bool IsTerminalError = false) =>
            new(false, Error: error, IsTerminalError: IsTerminalError);
    }
}
