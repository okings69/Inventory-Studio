using System.Security.Cryptography;
using System.Text;
using CourseInventory.Web.Data;
using CourseInventory.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Services;

public interface IInventoryApiTokenService
{
    Task<string> GenerateTokenAsync(int inventoryId, CancellationToken cancellationToken = default);
    string HashToken(string token);
}

public interface IInventoryAggregateExportService
{
    Task<InventoryAggregateExportDto?> BuildForTokenAsync(string token, CancellationToken cancellationToken = default);
}

public record InventoryAggregateExportDto(
    string InventoryTitle,
    IReadOnlyList<InventoryFieldExportDto> Fields,
    IReadOnlyList<NumericAggregateExportDto> NumericAggregates,
    IReadOnlyList<TextAggregateExportDto> TextAggregates);

public record InventoryFieldExportDto(string Title, string Type);

public record NumericAggregateExportDto(
    string Field,
    decimal? Min,
    decimal? Max,
    decimal? Average);

public record TextAggregateExportDto(
    string Field,
    IReadOnlyList<TextAggregateValueExportDto> Values);

public record TextAggregateValueExportDto(string Value, int Count);

public class InventoryApiTokenService(ApplicationDbContext db, TimeProvider timeProvider) : IInventoryApiTokenService
{
    public async Task<string> GenerateTokenAsync(int inventoryId, CancellationToken cancellationToken = default)
    {
        var inventory = await db.Inventories.FirstOrDefaultAsync(i => i.Id == inventoryId, cancellationToken);
        if (inventory is null)
        {
            throw new InvalidOperationException("Inventory not found.");
        }

        var token = GenerateRawToken();
        inventory.ApiTokenHash = HashToken(token);
        inventory.ApiTokenCreatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(bytes);
    }

    private static string GenerateRawToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

public class InventoryAggregateExportService(
    ApplicationDbContext db,
    IInventoryApiTokenService tokens) : IInventoryAggregateExportService
{
    public async Task<InventoryAggregateExportDto?> BuildForTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = tokens.HashToken(token);
        var inventory = await db.Inventories.AsNoTracking()
            .Where(i => i.ApiTokenHash == tokenHash)
            .Select(i => new { i.Id, i.Title })
            .FirstOrDefaultAsync(cancellationToken);

        if (inventory is null)
        {
            return null;
        }

        var fields = await db.InventoryFields.AsNoTracking()
            .Where(f => f.InventoryId == inventory.Id)
            .OrderBy(f => f.SortOrder)
            .Select(f => new ExportField(
                f.Title,
                f.FieldType,
                f.FieldKey))
            .ToListAsync(cancellationToken);

        var fieldDtos = fields
            .Select(f => new InventoryFieldExportDto(f.Title, ToExportType(f.FieldType)))
            .ToList();

        var numericFields = fields
            .Where(f => f.FieldType == InventoryFieldType.Number)
            .ToList();

        var textFields = fields
            .Where(f => f.FieldType is InventoryFieldType.SingleLineText
                or InventoryFieldType.MultiLineText
                or InventoryFieldType.Link
                or InventoryFieldType.Boolean)
            .ToList();

        var numericAggregates = numericFields.Count == 0
            ? []
            : await BuildNumericAggregatesAsync(inventory.Id, numericFields, cancellationToken);

        var textAggregates = textFields.Count == 0
            ? []
            : await BuildTextAggregatesAsync(inventory.Id, textFields, cancellationToken);

        return new InventoryAggregateExportDto(
            inventory.Title,
            fieldDtos,
            numericAggregates,
            textAggregates);
    }

    private async Task<List<NumericAggregateExportDto>> BuildNumericAggregatesAsync(
        int inventoryId,
        IReadOnlyList<ExportField> fields,
        CancellationToken cancellationToken)
    {
        var rows = await db.InventoryItems.AsNoTracking()
            .Where(i => i.InventoryId == inventoryId)
            .Select(i => new
            {
                i.Number1,
                i.Number2,
                i.Number3
            })
            .ToListAsync(cancellationToken);

        return fields
            .Select(field =>
            {
                var values = rows
                    .Select(row => ReadNumber(row.Number1, row.Number2, row.Number3, field.FieldKey))
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .ToList();

                return new NumericAggregateExportDto(
                    field.Title,
                    values.Count == 0 ? null : values.Min(),
                    values.Count == 0 ? null : values.Max(),
                    values.Count == 0 ? null : values.Average());
            })
            .ToList();
    }

    private async Task<List<TextAggregateExportDto>> BuildTextAggregatesAsync(
        int inventoryId,
        IReadOnlyList<ExportField> fields,
        CancellationToken cancellationToken)
    {
        var rows = await db.InventoryItems.AsNoTracking()
            .Where(i => i.InventoryId == inventoryId)
            .Select(i => new
            {
                i.Text1,
                i.Text2,
                i.Text3,
                i.LongText1,
                i.LongText2,
                i.LongText3,
                i.Link1,
                i.Link2,
                i.Link3,
                i.Bool1,
                i.Bool2,
                i.Bool3
            })
            .ToListAsync(cancellationToken);

        return fields
            .Select(field =>
            {
                var values = rows
                    .Select(row => ReadTextValue(
                        row.Text1,
                        row.Text2,
                        row.Text3,
                        row.LongText1,
                        row.LongText2,
                        row.LongText3,
                        row.Link1,
                        row.Link2,
                        row.Link3,
                        row.Bool1,
                        row.Bool2,
                        row.Bool3,
                        field.FieldKey))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .GroupBy(value => value!)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key)
                    .Take(10)
                    .Select(group => new TextAggregateValueExportDto(group.Key, group.Count()))
                    .ToList();

                return new TextAggregateExportDto(field.Title, values);
            })
            .ToList();
    }

    private static decimal? ReadNumber(decimal? number1, decimal? number2, decimal? number3, string fieldKey) =>
        fieldKey switch
        {
            "Number1" => number1,
            "Number2" => number2,
            "Number3" => number3,
            _ => null
        };

    private static string? ReadTextValue(
        string? text1,
        string? text2,
        string? text3,
        string? longText1,
        string? longText2,
        string? longText3,
        string? link1,
        string? link2,
        string? link3,
        bool? bool1,
        bool? bool2,
        bool? bool3,
        string fieldKey) =>
        fieldKey switch
        {
            "Text1" => text1,
            "Text2" => text2,
            "Text3" => text3,
            "LongText1" => longText1,
            "LongText2" => longText2,
            "LongText3" => longText3,
            "Link1" => link1,
            "Link2" => link2,
            "Link3" => link3,
            "Bool1" => bool1?.ToString(),
            "Bool2" => bool2?.ToString(),
            "Bool3" => bool3?.ToString(),
            _ => null
        };

    private static string ToExportType(InventoryFieldType type) =>
        type switch
        {
            InventoryFieldType.SingleLineText => "Text",
            InventoryFieldType.MultiLineText => "Text",
            InventoryFieldType.Number => "Number",
            InventoryFieldType.Link => "Text",
            InventoryFieldType.Boolean => "Text",
            _ => type.ToString()
        };

    private sealed record ExportField(
        string Title,
        InventoryFieldType FieldType,
        string FieldKey);
}
