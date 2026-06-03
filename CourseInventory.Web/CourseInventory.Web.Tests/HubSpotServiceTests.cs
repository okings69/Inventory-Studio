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
    public async Task SendProfileAsync_WhenHubSpotRejectsToken_ReturnsAuthenticationError()
    {
        var handler = new QueueHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, "{}");
        var service = CreateService(handler);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.False(result.Success);
        Assert.Equal("HubSpot authentication failed", result.Error);
    }

    [Fact]
    public async Task SendProfileAsync_CreatesCompanyContactAndAssociation()
    {
        var handler = new QueueHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Created, """{"id":"company-123"}""");
        handler.Enqueue(HttpStatusCode.Created, """{"id":"contact-456"}""");
        handler.Enqueue(HttpStatusCode.NoContent, "");
        var service = CreateService(handler);

        var result = await service.SendProfileAsync(CreateUser(), CreateForm());

        Assert.True(result.Success);
        Assert.Equal("company-123", result.CompanyId);
        Assert.Equal("contact-456", result.ContactId);
        Assert.True(result.AssociationCreated);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("/crm/v3/objects/companies", handler.Requests[0].RequestUri!.PathAndQuery);
        Assert.Equal("/crm/v3/objects/contacts", handler.Requests[1].RequestUri!.PathAndQuery);
        Assert.Equal("/crm/v4/objects/contact/contact-456/associations/default/company/company-123", handler.Requests[2].RequestUri!.PathAndQuery);
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("test-token", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    private static HubSpotService CreateService(QueueHttpMessageHandler handler, string? accessToken = "test-token") =>
        new(
            new HttpClient(handler),
            Options.Create(new HubSpotOptions { AccessToken = accessToken }),
            NullLogger<HubSpotService>.Instance);

    private static ApplicationUser CreateUser() => new()
    {
        Id = "user-id",
        UserName = "Orian",
        Email = "orian@example.com",
        PhoneNumber = "+375333583012"
    };

    private static HubSpotProfileFormViewModel CreateForm() => new()
    {
        UserId = "user-id",
        UserName = "Orian",
        UserEmail = "orian@example.com",
        CompanyName = "Inventory Studio Demo",
        Phone = "+375333583012",
        JobTitle = "Inventory manager",
        City = "Minsk",
        Country = "Belarus",
        Notes = "Course demo"
    };

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

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
