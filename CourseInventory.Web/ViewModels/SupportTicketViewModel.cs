using System.ComponentModel.DataAnnotations;

namespace CourseInventory.Web.ViewModels;

public class SupportTicketViewModel
{
    [Required]
    public int InventoryId { get; set; }

    public string InventoryTitle { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Low|Average|High)$", ErrorMessage = "Priority must be Low, Average, or High.")]
    public string Priority { get; set; } = "Average";

    [Required, StringLength(2000)]
    public string Summary { get; set; } = string.Empty;
}
