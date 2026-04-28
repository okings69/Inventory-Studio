using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CourseInventory.Web.Models.Inventory;

public class Inventory
{
    public const string DefaultStatusOptions = "Available, In use, Broken";

    public int Id { get; set; }

    [Required, StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [StringLength(8000)]
    public string DescriptionMarkdown { get; set; } = string.Empty;

    [NotMapped]
    public string Description
    {
        get => DescriptionMarkdown;
        set => DescriptionMarkdown = value;
    }

    [NotMapped]
    public int ItemCount { get; set; }

    [Required, StringLength(80)]
    public string Category { get; set; } = "General";

    [StringLength(600)]
    public string? ImageUrl { get; set; }

    [StringLength(500)]
    public string StatusOptions { get; set; } = DefaultStatusOptions;

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    public ApplicationUser? Owner { get; set; }

    public bool IsPublic { get; set; }

    [Timestamp]
    public uint RowVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InventoryItem> Items { get; set; } = [];
    public ICollection<InventoryField> Fields { get; set; } = [];
    public ICollection<CustomIdElement> CustomIdElements { get; set; } = [];
    public ICollection<InventoryAccess> Accesses { get; set; } = [];
    public ICollection<DiscussionMessage> Messages { get; set; } = [];
    public ICollection<InventoryTag> InventoryTags { get; set; } = [];
}
