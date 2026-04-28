using System.ComponentModel.DataAnnotations;

namespace CourseInventory.Web.Models.Inventory;

public class InventoryItem
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public Inventory? Inventory { get; set; }

    [StringLength(120)]
    public string? CustomId { get; set; }

    [Required]
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public uint RowVersion { get; set; }

    [StringLength(500)] public string? Text1 { get; set; }
    [StringLength(500)] public string? Text2 { get; set; }
    [StringLength(500)] public string? Text3 { get; set; }
    [StringLength(8000)] public string? LongText1 { get; set; }
    [StringLength(8000)] public string? LongText2 { get; set; }
    [StringLength(8000)] public string? LongText3 { get; set; }
    public decimal? Number1 { get; set; }
    public decimal? Number2 { get; set; }
    public decimal? Number3 { get; set; }
    [StringLength(1000)] public string? Link1 { get; set; }
    [StringLength(1000)] public string? Link2 { get; set; }
    [StringLength(1000)] public string? Link3 { get; set; }
    public bool? Bool1 { get; set; }
    public bool? Bool2 { get; set; }
    public bool? Bool3 { get; set; }

    public ICollection<ItemLike> Likes { get; set; } = [];
}
