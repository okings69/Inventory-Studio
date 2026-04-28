using System.ComponentModel.DataAnnotations;

namespace CourseInventory.Web.Models.Inventory;

public class DiscussionMessage
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public Inventory? Inventory { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }

    [Required, StringLength(4000)]
    public string BodyMarkdown { get; set; } = string.Empty;

    [Required]
    public string BodyHtml { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
