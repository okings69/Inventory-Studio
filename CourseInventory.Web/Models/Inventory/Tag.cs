using System.ComponentModel.DataAnnotations;

namespace CourseInventory.Web.Models.Inventory;

public class Tag
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string NormalizedName { get; set; } = string.Empty;

    public ICollection<InventoryTag> InventoryTags { get; set; } = [];
}
