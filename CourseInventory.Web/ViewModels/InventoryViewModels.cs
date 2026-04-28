using System.ComponentModel.DataAnnotations;
using CourseInventory.Web.Models.Inventory;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CourseInventory.Web.ViewModels;

public class InventoryFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(180)]
    public string Title { get; set; } = string.Empty;

    public string DescriptionMarkdown { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string Category { get; set; } = "Equipment";

    [Url, StringLength(600)]
    [Display(Name = "Image URL")]
    public string? ImageUrl { get; set; }

    [StringLength(500)]
    public string StatusOptions { get; set; } = Inventory.DefaultStatusOptions;

    public bool IsPublic { get; set; }
    public uint RowVersion { get; set; }
    public string? Tags { get; set; }
}

public class InventoryDetailsViewModel
{
    public Inventory Inventory { get; set; } = new();
    public AccessState Access { get; set; } = new(false, false, false, false);
    public InventoryStats? Stats { get; set; }
    public string CustomIdPreview { get; set; } = string.Empty;
}

public class ItemFormViewModel
{
    public InventoryItem Item { get; set; } = new();

    [ValidateNever]
    public Inventory Inventory { get; set; } = new();

    [ValidateNever]
    public IReadOnlyList<InventoryField> Fields { get; set; } = [];
}
