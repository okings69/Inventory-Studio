namespace CourseInventory.Web.Models.Inventory;

public class ItemLike
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public InventoryItem? Item { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
