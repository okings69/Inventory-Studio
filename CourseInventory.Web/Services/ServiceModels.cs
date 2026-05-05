using CourseInventory.Web.Models.Inventory;

namespace CourseInventory.Web.Services;

public record AccessState(bool CanRead, bool CanWrite, bool CanManage, bool IsAdmin);
public record AccessScope(bool IsAuthenticated, bool IsAdmin, string? UserId);
public record ServiceResult(bool Success, string? Error = null)
{
    public static ServiceResult Ok() => new(true);
    public static ServiceResult Fail(string error) => new(false, error);
}

public record ValueServiceResult<T>(bool Success, T? Value = default, string? Error = null)
{
    public static ValueServiceResult<T> Ok(T value) => new(true, value);
    public static ValueServiceResult<T> Fail(string error) => new(false, default, error);
}

public record SearchResultItem(string Type, int Id, string Title, string? Snippet, string Url);
public record InventoryStats(
    int ItemCount,
    int TotalLikes,
    IReadOnlyDictionary<string, (decimal? Min, decimal? Max, decimal? Average)> NumberStats,
    IReadOnlyDictionary<string, IReadOnlyList<(string Value, int Count)>> FrequentTextValues);

public record FieldSlot(InventoryFieldType Type, string Key);
