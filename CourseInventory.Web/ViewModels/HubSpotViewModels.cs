using System.ComponentModel.DataAnnotations;

namespace CourseInventory.Web.ViewModels;

public class HubSpotProfileFormViewModel
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public string? CompanyId { get; set; }
    public string? ContactId { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public bool AssociationCompleted { get; set; }
    public string? HubSpotCompanyUrl { get; set; }
    public string? HubSpotContactUrl { get; set; }

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
