using Microsoft.AspNetCore.Identity;

namespace CourseInventory.Web.Models;

public class ApplicationUser : IdentityUser
{
    public bool IsBlocked { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public string PreferredTheme { get; set; } = "light";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Inventory.Inventory> OwnedInventories { get; set; } = [];
}
