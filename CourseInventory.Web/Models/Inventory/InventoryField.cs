using System.ComponentModel.DataAnnotations;

namespace CourseInventory.Web.Models.Inventory;

public class InventoryField
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public Inventory? Inventory { get; set; }
    public InventoryFieldType FieldType { get; set; }

    [Required, StringLength(32)]
    public string FieldKey { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [StringLength(600)]
    public string? Description { get; set; }

    public bool ShowInTable { get; set; } = true;
    public int SortOrder { get; set; }
}
