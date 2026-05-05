namespace CourseInventory.Web.Models;

public class UserLoginActivity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public DateTime LoggedInAt { get; set; } = DateTime.UtcNow;
}
