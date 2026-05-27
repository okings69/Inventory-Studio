using System.ComponentModel.DataAnnotations;

namespace CourseInventory.Web.ViewModels;

public class SalesforceProfileFormViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string? UserEmail { get; set; }

    [Required, StringLength(160)]
    [Display(Name = "Company name")]
    public string CompanyName { get; set; } = string.Empty;

    [Phone, StringLength(40)]
    public string? Phone { get; set; }

    [StringLength(120)]
    [Display(Name = "Job title")]
    public string? JobTitle { get; set; }

    [StringLength(80)]
    public string? City { get; set; }

    [StringLength(80)]
    public string? Country { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
