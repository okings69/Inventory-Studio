using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CourseInventory.Tests.TestSupport;

internal static class TestDb
{
    public static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"inventory-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    public static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    public static async Task<ApplicationUser> AddUserAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        string userName,
        bool isAdmin = false)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@example.com",
            NormalizedEmail = $"{userName}@example.com".ToUpperInvariant(),
            EmailConfirmed = true
        };

        db.Users.Add(user);

        if (isAdmin)
        {
            var role = new IdentityRole("Admin")
            {
                NormalizedName = "ADMIN"
            };
            db.Roles.Add(role);
            db.UserRoles.Add(new IdentityUserRole<string>
            {
                UserId = user.Id,
                RoleId = role.Id
            });
        }

        await db.SaveChangesAsync();
        return user;
    }

    public static AccessService CreateAccessService(ApplicationDbContext db, UserManager<ApplicationUser> users) =>
        new(db, users);
}
