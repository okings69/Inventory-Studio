namespace CourseInventory.Web.Services;

public static class GoogleOAuthConfiguration
{
    public static bool HasUsableGoogleOAuthCredentials(IConfiguration configuration) =>
        IsLikelyGoogleOAuthClientId(configuration["Authentication:Google:ClientId"]) &&
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);

    public static bool IsLikelyGoogleOAuthClientId(string? clientId) =>
        !string.IsNullOrWhiteSpace(clientId) &&
        clientId.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase);
}
