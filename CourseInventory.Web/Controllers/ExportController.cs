using System.Globalization;
using System.Text;
using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using CourseInventory.Web.Models.Inventory;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Controllers;

public class ExportController(
    ApplicationDbContext db,
    IAccessService access,
    UserManager<ApplicationUser> users) : Controller
{
    private const string ExcelDelimiter = ";";
    private const string ExportDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public async Task<IActionResult> Csv(int inventoryId)
    {
        var user = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        var accessState = await access.GetAccessAsync(inventoryId, user);
        if (!accessState.CanRead)
        {
            return user is null ? NotFound() : Forbid();
        }

        var inventory = await db.Inventories.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory is null)
        {
            return NotFound();
        }

        var fields = await db.InventoryFields.AsNoTracking()
            .Where(f => f.InventoryId == inventoryId)
            .OrderBy(f => f.SortOrder)
            .ToListAsync();

        var items = await db.InventoryItems.AsNoTracking()
            .Where(i => i.InventoryId == inventoryId)
            .Include(i => i.Likes)
            .OrderBy(i => i.Id)
            .ToListAsync();

        var exportFields = fields
            .Where(field => field.ShowInTable || items.Any(item => HasUsefulValue(GetFieldValue(item, field.FieldKey))))
            .ToList();

        var rows = new List<string>
        {
            $"sep={ExcelDelimiter}",
            BuildCsvRow([
                "Id",
                "Custom ID",
                .. exportFields.Select(field => field.Title),
                "Likes",
                "Updated"
            ])
        };

        foreach (var item in items)
        {
            var values = new List<string?>
            {
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.CustomId
            };

            values.AddRange(exportFields.Select(field => FormatFieldValue(GetFieldValue(item, field.FieldKey))));
            values.Add(item.Likes.Count.ToString(CultureInfo.InvariantCulture));
            values.Add(item.UpdatedAt.ToLocalTime().ToString(ExportDateTimeFormat, CultureInfo.InvariantCulture));

            rows.Add(BuildCsvRow(values));
        }

        var csvContent = string.Join(Environment.NewLine, rows);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var fileBytes = encoding.GetBytes(csvContent);
        var fileName = $"{SanitizeFileName(inventory.Title)}.csv";

        return File(fileBytes, "text/csv; charset=utf-8", fileName);
    }

    private static object? GetFieldValue(InventoryItem item, string fieldKey) => fieldKey switch
    {
        "Text1" => item.Text1,
        "Text2" => item.Text2,
        "Text3" => item.Text3,
        "LongText1" => item.LongText1,
        "LongText2" => item.LongText2,
        "LongText3" => item.LongText3,
        "Number1" => item.Number1,
        "Number2" => item.Number2,
        "Number3" => item.Number3,
        "Link1" => item.Link1,
        "Link2" => item.Link2,
        "Link3" => item.Link3,
        "Bool1" => item.Bool1,
        "Bool2" => item.Bool2,
        "Bool3" => item.Bool3,
        _ => null
    };

    private static string FormatFieldValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text;
        }

        if (value is decimal decimalValue)
        {
            return decimalValue.ToString("0.##", CultureInfo.CurrentCulture);
        }

        if (value is bool booleanValue)
        {
            return booleanValue ? "True" : "False";
        }

        return value.ToString() ?? string.Empty;
    }

    private static bool HasUsefulValue(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            return !string.IsNullOrWhiteSpace(text);
        }

        if (value is decimal || value is bool)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(value.ToString());
    }

    private static string BuildCsvRow(IEnumerable<string?> values) =>
        string.Join(ExcelDelimiter, values.Select(EscapeCsvValue));

    private static string EscapeCsvValue(string? value)
    {
        var text = ProtectExcelFormula(value ?? string.Empty);
        var mustQuote = text.Contains(ExcelDelimiter, StringComparison.Ordinal)
            || text.Contains('"')
            || text.Contains('\r')
            || text.Contains('\n');

        if (!mustQuote)
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string ProtectExcelFormula(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value[0] is '=' or '+' or '-' or '@'
            ? $"'{value}"
            : value;
    }

    private static string SanitizeFileName(string title)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "inventory-export" : cleaned;
    }
}
