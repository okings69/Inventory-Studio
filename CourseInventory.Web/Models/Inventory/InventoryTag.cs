namespace CourseInventory.Web.Models.Inventory;

public class InventoryTag
{
    public int InventoryId { get; set; }
    public Inventory? Inventory { get; set; }
    public int TagId { get; set; }
    public Tag? Tag { get; set; }
}
