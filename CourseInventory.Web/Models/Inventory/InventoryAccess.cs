namespace CourseInventory.Web.Models.Inventory;

public class InventoryAccess
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public Inventory? Inventory { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
