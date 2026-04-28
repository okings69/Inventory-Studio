using CourseInventory.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var inventoryId = args.Length > 0 && int.TryParse(args[0], out var parsedInventoryId)
    ? parsedInventoryId
    : 5;

var config = new ConfigurationBuilder()
    .SetBasePath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CourseInventory.Web"))
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var connectionString = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing connection string.");

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(connectionString)
    .EnableSensitiveDataLogging()
    .Options;

await using var db = new ApplicationDbContext(options);

var inventory = await db.Inventories.AsNoTracking()
    .Where(i => i.Id == inventoryId)
    .Select(i => new
    {
        i.Id,
        i.Title,
        i.StatusOptions
    })
    .FirstOrDefaultAsync();

Console.WriteLine(inventory is null
    ? $"Inventory {inventoryId}: not found"
    : $"Inventory {inventory.Id}: {inventory.Title} | StatusOptions={inventory.StatusOptions}");

var items = await db.InventoryItems.AsNoTracking()
    .Where(i => i.InventoryId == inventoryId)
    .OrderByDescending(i => i.Id)
    .Select(i => new
    {
        i.Id,
        i.InventoryId,
        i.CustomId,
        i.Text1,
        i.Text2,
        i.Text3,
        i.LongText1,
        i.LongText2,
        i.LongText3,
        i.Number1,
        i.Number2,
        i.Number3,
        i.Link1,
        i.Link2,
        i.Link3
    })
    .ToListAsync();

Console.WriteLine($"Items for inventory {inventoryId}: {items.Count}");

foreach (var item in items)
{
    Console.WriteLine(
        $"Item {item.Id} | InventoryId={item.InventoryId} | CustomId={item.CustomId} | " +
        $"T1={item.Text1} | T2={item.Text2} | T3={item.Text3} | " +
        $"LT1={item.LongText1} | LT2={item.LongText2} | LT3={item.LongText3} | " +
        $"N1={item.Number1} | N2={item.Number2} | N3={item.Number3} | " +
        $"L1={item.Link1} | L2={item.Link2} | L3={item.Link3}");
}
