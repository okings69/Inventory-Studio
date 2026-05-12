using CourseInventory.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CourseInventory.Web.Data;

public static class SeedData
{
    public static async Task SeedAsync(
        IServiceProvider services,
        bool throwOnMigrationFailure = true,
        bool throwOnSeedFailure = true)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SeedData");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        logger.LogInformation("Starting database migration");
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migration completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed");
            if (throwOnMigrationFailure)
            {
                throw;
            }

            logger.LogWarning("Skipping seed because database migration failed and throwOnMigrationFailure is false");
            return;
        }

        logger.LogInformation("Starting seed");
        try
        {
            await SeedRolesAndAdminAsync(scope.ServiceProvider, logger);
            logger.LogInformation("Seed completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seed failed");
            if (throwOnSeedFailure)
            {
                throw;
            }
        }
    }

    private static async Task SeedRolesAndAdminAsync(IServiceProvider services, ILogger logger)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        foreach (var role in new[] { "Admin", "User" })
        {
            if (await roles.RoleExistsAsync(role))
            {
                continue;
            }

            var roleResult = await roles.CreateAsync(new IdentityRole(role));
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException($"Role '{role}' could not be created: {FormatErrors(roleResult.Errors)}");
            }
        }

        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];
        var adminDisplayName = configuration["SeedAdmin:DisplayName"];
        var resetAdminPassword = configuration.GetValue<bool>("SeedAdmin:ResetPassword");

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("SeedAdmin__Email or SeedAdmin__Password is missing. Admin seed skipped.");
            return;
        }

        var adminUserName = BuildAdminUserName(adminDisplayName);
        var admin = await users.FindByEmailAsync(adminEmail)
            ?? await users.FindByNameAsync(adminUserName)
            ?? await users.FindByNameAsync("admin");

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminUserName,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createResult = await users.CreateAsync(admin, adminPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Seed admin could not be created: {FormatErrors(createResult.Errors)}");
            }
        }

        admin.Email = adminEmail;
        admin.EmailConfirmed = true;
        admin.IsBlocked = false;
        var updateResult = await users.UpdateAsync(admin);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException($"Seed admin could not be updated: {FormatErrors(updateResult.Errors)}");
        }

        if (!await users.IsInRoleAsync(admin, "Admin"))
        {
            var adminRoleResult = await users.AddToRoleAsync(admin, "Admin");
            if (!adminRoleResult.Succeeded)
            {
                throw new InvalidOperationException($"Seed admin role could not be assigned: {FormatErrors(adminRoleResult.Errors)}");
            }
        }

        if (resetAdminPassword)
        {
            await users.RemovePasswordAsync(admin);
            var passwordResult = await users.AddPasswordAsync(admin, adminPassword);
            if (!passwordResult.Succeeded)
            {
                throw new InvalidOperationException($"Seed admin password could not be reset: {FormatErrors(passwordResult.Errors)}");
            }
        }

        if (!await users.IsInRoleAsync(admin, "User"))
        {
            var userRoleResult = await users.AddToRoleAsync(admin, "User");
            if (!userRoleResult.Succeeded)
            {
                throw new InvalidOperationException($"Seed admin user role could not be assigned: {FormatErrors(userRoleResult.Errors)}");
            }
        }
    }

    private static string BuildAdminUserName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "admin";
        }

        var cleaned = new string(displayName
            .Trim()
            .Where(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or '@')
            .ToArray());

        return string.IsNullOrWhiteSpace(cleaned) ? "admin" : cleaned;
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors) =>
        string.Join("; ", errors.Select(error => error.Description));
}
