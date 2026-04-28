using System.ComponentModel.DataAnnotations;

namespace CourseInventory.Web.Models.Inventory;

public class CustomIdElement
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public Inventory? Inventory { get; set; }
    public CustomIdElementType ElementType { get; set; }

    [StringLength(80)]
    public string? Format { get; set; }

    [StringLength(200)]
    public string? FixedValue { get; set; }

    public int SortOrder { get; set; }
}
