using System.ComponentModel.DataAnnotations;

namespace CourseInventory.Web.ViewModels;

public class HubSpotProfileFormViewModel
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }

    [Required, StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Phone, StringLength(40)]
    public string? Phone { get; set; }

    [StringLength(120)]
    public string? JobTitle { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(120)]
    public string? Country { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
