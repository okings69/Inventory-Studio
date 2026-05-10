using CourseInventory.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CourseInventory.Web.Data;

public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];
        var resetAdminPassword = configuration.GetValue<bool>("SeedAdmin:ResetPassword");

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var admin = await users.FindByEmailAsync(adminEmail) ?? await users.FindByNameAsync("admin");
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = "admin",
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createResult = await users.CreateAsync(admin, adminPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Seed admin could not be created: {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        admin.Email = adminEmail;
        admin.EmailConfirmed = true;
        admin.IsBlocked = false;
        await users.UpdateAsync(admin);

        if (!await users.IsInRoleAsync(admin, "Admin"))
        {
            await users.AddToRoleAsync(admin, "Admin");
        }

        if (resetAdminPassword)
        {
            await users.RemovePasswordAsync(admin);
            var passwordResult = await users.AddPasswordAsync(admin, adminPassword);
            if (!passwordResult.Succeeded)
            {
                throw new InvalidOperationException($"Seed admin password could not be reset: {string.Join("; ", passwordResult.Errors.Select(e => e.Description))}");
            }
        }

        if (!await users.IsInRoleAsync(admin, "User"))
        {
            await users.AddToRoleAsync(admin, "User");
        }
    }
}
