namespace CourseInventory.Web.Services;

public class SalesforceOptions
{
    public const string PasswordFlow = "Password";
    public const string ClientCredentialsFlow = "ClientCredentials";

    public string AuthFlow { get; set; } = PasswordFlow;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SecurityToken { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = "https://login.salesforce.com";
    public string ApiVersion { get; set; } = "60.0";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(LoginUrl) &&
        (UsesClientCredentials ||
            (!string.IsNullOrWhiteSpace(Username) &&
             !string.IsNullOrWhiteSpace(Password)));

    public bool UsesClientCredentials =>
        string.Equals(AuthFlow, ClientCredentialsFlow, StringComparison.OrdinalIgnoreCase);
}
