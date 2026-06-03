using System.Net;
using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using CourseInventory.Web.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CourseInventory.Web.Tests;

public class HubSpotServiceTests
{
    [Fact]
    public async Task SendProfileAsync_WhenAccessTokenMissing_ReturnsConfiguredError()
    {
        var service = CreateService(new QueueHttpMessageHandler(), accessToken: null);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.False(result.Success);
        Assert.Equal("HubSpot is not configured", result.Error);
    }

    [Fact]
    public async Task SendProfileAsync_CreatesCompanyContactAndAssociation()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueOk(SearchResults());
        handler.EnqueueOk(SearchResults());
        handler.Enqueue(HttpStatusCode.Created, """{"id":"company-123"}""");
        handler.EnqueueOk(SearchResults());
        handler.Enqueue(HttpStatusCode.Created, """{"id":"contact-456"}""");
        handler.Enqueue(HttpStatusCode.NoContent, "");
        var service = CreateService(handler);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.True(result.Success);
        Assert.Equal("company-123", result.CompanyId);
        Assert.Equal("contact-456", result.ContactId);
        Assert.True(result.AssociationCreated);
        Assert.Equal("https://app.hubspot.com/contacts/999999/record/0-2/company-123", result.HubSpotCompanyUrl);
        Assert.Equal("https://app.hubspot.com/contacts/999999/record/0-1/contact-456", result.HubSpotContactUrl);
        Assert.Equal("/crm/v3/objects/companies/search", handler.Requests[0].RequestUri!.PathAndQuery);
        Assert.Equal("/crm/v3/objects/companies/search", handler.Requests[1].RequestUri!.PathAndQuery);
        Assert.Equal("/crm/v3/objects/companies", handler.Requests[2].RequestUri!.PathAndQuery);
        Assert.Equal("/crm/v3/objects/contacts/search", handler.Requests[3].RequestUri!.PathAndQuery);
        Assert.Equal("/crm/v3/objects/contacts", handler.Requests[4].RequestUri!.PathAndQuery);
        Assert.Equal("/crm/v4/objects/contact/contact-456/associations/default/company/company-123", handler.Requests[5].RequestUri!.PathAndQuery);
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("test-token", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task SendProfileAsync_WhenContactAlreadyExists_ReusesExistingContact()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueOk(SearchResults());
        handler.EnqueueOk(SearchResults());
        handler.Enqueue(HttpStatusCode.Created, """{"id":"company-123"}""");
        handler.EnqueueOk(SearchResults("contact-456"));
        handler.Enqueue(HttpStatusCode.NoContent, "");
        var service = CreateService(handler);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.True(result.Success);
        Assert.Equal("contact-456", result.ContactId);
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri!.PathAndQuery == "/crm/v3/objects/contacts");
    }

    [Fact]
    public async Task SendProfileAsync_WhenCompanyAlreadyExists_ReusesExistingCompany()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueOk(SearchResults("company-123"));
        handler.EnqueueOk(SearchResults());
        handler.Enqueue(HttpStatusCode.Created, """{"id":"contact-456"}""");
        handler.Enqueue(HttpStatusCode.NoContent, "");
        var service = CreateService(handler);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.True(result.Success);
        Assert.Equal("company-123", result.CompanyId);
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri!.PathAndQuery == "/crm/v3/objects/companies");
    }

    [Fact]
    public async Task SendProfileAsync_WhenHubSpotReturnsConflict_ReusesExistingRecord()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueOk(SearchResults());
        handler.EnqueueOk(SearchResults());
        handler.Enqueue(HttpStatusCode.Conflict, """{"message":"duplicate"}""");
        handler.EnqueueOk(SearchResults("company-123"));
        handler.EnqueueOk(SearchResults("contact-456"));
        handler.Enqueue(HttpStatusCode.NoContent, "");
        var service = CreateService(handler);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.True(result.Success);
        Assert.Equal("company-123", result.CompanyId);
        Assert.Equal("contact-456", result.ContactId);
    }

    [Fact]
    public async Task SendProfileAsync_WhenUnauthorized_ReturnsInvalidTokenError()
    {
        var handler = new QueueHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, "{}");
        var service = CreateService(handler);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.False(result.Success);
        Assert.Equal("HubSpot access token is invalid or expired.", result.Error);
    }

    [Fact]
    public async Task SendProfileAsync_WhenForbidden_ReturnsPermissionError()
    {
        var handler = new QueueHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Forbidden, "{}");
        var service = CreateService(handler);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.False(result.Success);
        Assert.Equal("HubSpot permissions are insufficient. Check CRM scopes.", result.Error);
    }

    [Fact]
    public async Task SendProfileAsync_WhenValidationFails_ReturnsHubSpotValidationMessage()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueOk(SearchResults());
        handler.EnqueueOk(SearchResults());
        handler.Enqueue(HttpStatusCode.BadRequest, """{"message":"Property values were not valid"}""");
        var service = CreateService(handler);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.False(result.Success);
        Assert.Equal("HubSpot validation failed: Property values were not valid", result.Error);
    }

    [Fact]
    public async Task SendProfileAsync_WhenAssociationFails_ReturnsAssociationErrorWithIds()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueOk(SearchResults());
        handler.EnqueueOk(SearchResults());
        handler.Enqueue(HttpStatusCode.Created, """{"id":"company-123"}""");
        handler.EnqueueOk(SearchResults());
        handler.Enqueue(HttpStatusCode.Created, """{"id":"contact-456"}""");
        handler.Enqueue(HttpStatusCode.BadRequest, """{"message":"Association failed"}""");
        var service = CreateService(handler);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.False(result.Success);
        Assert.Equal("Contact was created but could not be associated with the company.", result.Error);
        Assert.Equal("company-123", result.CompanyId);
        Assert.Equal("contact-456", result.ContactId);
        Assert.False(result.AssociationCreated);
    }

    private static HubSpotService CreateService(QueueHttpMessageHandler handler, string? accessToken = "test-token") =>
        new(
            new HttpClient(handler),
            Options.Create(new HubSpotOptions { AccessToken = accessToken, PortalId = "999999" }),
            NullLogger<HubSpotService>.Instance);

    private static ApplicationUser CreateUser() => new()
    {
        Id = "user-id",
        UserName = "Orian Demo",
        Email = "orian@example.com",
        PhoneNumber = "+375333583012"
    };

    private static HubSpotProfileFormViewModel CreateForm() => new()
    {
        UserId = "user-id",
        UserName = "Orian Demo",
        UserEmail = "orian@example.com",
        CompanyName = "Inventory Studio Demo",
        Phone = "+375333583012",
        JobTitle = "Inventory manager",
        City = "Minsk",
        Country = "Belarus",
        Notes = "Course demo"
    };

    private static string SearchResults(string? id = null) =>
        id is null
            ? """{"total":0,"results":[]}"""
            : $$"""{"total":1,"results":[{"id":"{{id}}"}]}""";

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public void EnqueueOk(string content) => Enqueue(HttpStatusCode.OK, content);

        public void Enqueue(HttpStatusCode statusCode, string content)
        {
            responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responses.Count > 0
                ? responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
