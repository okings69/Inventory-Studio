using System.ComponentModel.DataAnnotations;

namespace CourseInventory.Web.ViewModels;

public class RegisterViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string UserName { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required]
    public string EmailOrUserName { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
