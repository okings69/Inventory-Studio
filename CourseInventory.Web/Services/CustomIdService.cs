using System.Security.Cryptography;
using System.Text;
using CourseInventory.Web.Data;
using CourseInventory.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Services;

public interface ICustomIdService
{
    Task<string> GenerateAsync(int inventoryId);
    Task<string> PreviewAsync(int inventoryId);
    Task<ServiceResult> ValidateElementsAsync(IEnumerable<CustomIdElement> elements);
}

public class CustomIdService(ApplicationDbContext db) : ICustomIdService
{
    public async Task<string> GenerateAsync(int inventoryId)
    {
        var elements = await db.CustomIdElements.AsNoTracking()
            .Where(e => e.InventoryId == inventoryId)
            .OrderBy(e => e.SortOrder)
            .ToListAsync();

        if (elements.Count == 0)
        {
            elements.Add(new CustomIdElement { ElementType = CustomIdElementType.FixedText, FixedValue = "ITEM-" });
            elements.Add(new CustomIdElement { ElementType = CustomIdElementType.Sequence, Format = "D5" });
        }

        var sequence = await db.InventoryItems.CountAsync(i => i.InventoryId == inventoryId) + 1;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var value = Build(elements, sequence + attempt);
            if (!await db.InventoryItems.AnyAsync(i => i.InventoryId == inventoryId && i.CustomId == value))
            {
                return value;
            }
        }
        throw new InvalidOperationException("Unable to generate a unique CustomId for this inventory.");
    }

    public async Task<string> PreviewAsync(int inventoryId)
    {
        var elements = await db.CustomIdElements.AsNoTracking()
            .Where(e => e.InventoryId == inventoryId)
            .OrderBy(e => e.SortOrder)
            .ToListAsync();
        return elements.Count == 0 ? "ITEM-00001" : Build(elements, 1);
    }

    public Task<ServiceResult> ValidateElementsAsync(IEnumerable<CustomIdElement> elements)
    {
        foreach (var element in elements)
        {
            if (element.ElementType == CustomIdElementType.FixedText && string.IsNullOrWhiteSpace(element.FixedValue))
                return Task.FromResult(ServiceResult.Fail("Fixed text elements need a value."));
            if (element.ElementType == CustomIdElementType.DateTime && string.IsNullOrWhiteSpace(element.Format))
                return Task.FromResult(ServiceResult.Fail("Date/time elements need a .NET date format."));
        }
        return Task.FromResult(ServiceResult.Ok());
    }

    private static string Build(IEnumerable<CustomIdElement> elements, int sequence)
    {
        var sb = new StringBuilder();
        foreach (var e in elements)
        {
            sb.Append(e.ElementType switch
            {
                CustomIdElementType.FixedText => e.FixedValue ?? string.Empty,
                CustomIdElementType.Random20Bit => RandomNumberGenerator.GetInt32(1 << 20).ToString("X5"),
                CustomIdElementType.Random32Bit => RandomNumberGenerator.GetInt32(int.MaxValue).ToString("X8"),
                CustomIdElementType.Random6Digits => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"),
                CustomIdElementType.Random9Digits => RandomNumberGenerator.GetInt32(0, 1_000_000_000).ToString("D9"),
                CustomIdElementType.Guid => Guid.NewGuid().ToString(e.Format is "N" or "D" ? e.Format : "N"),
                CustomIdElementType.DateTime => DateTime.UtcNow.ToString(string.IsNullOrWhiteSpace(e.Format) ? "yyyyMMdd" : e.Format.Trim()),
                CustomIdElementType.Sequence => FormatSequence(sequence, e.Format),
                _ => string.Empty
            });
        }
        return sb.ToString();
    }

    private static string FormatSequence(int sequence, string? format)
    {
        var effectiveFormat = string.IsNullOrWhiteSpace(format) ? "D5" : format.Trim();
        if (!TryGetDecimalWidth(effectiveFormat, out var width))
        {
            return sequence.ToString(effectiveFormat);
        }

        var digits = sequence.ToString();
        if (digits.Length > width)
        {
            throw new InvalidOperationException($"Sequence value {sequence} exceeds format {effectiveFormat}. Use a wider sequence format.");
        }

        return sequence.ToString($"D{width}");
    }

    private static bool TryGetDecimalWidth(string format, out int width)
    {
        width = 0;
        if (format.Length < 2 || format[0] is not ('D' or 'd'))
        {
            return false;
        }

        foreach (var c in format[1..])
        {
            if (!char.IsDigit(c))
            {
                width = 0;
                return false;
            }

            width = (width * 10) + (c - '0');
        }

        return true;
    }
}
