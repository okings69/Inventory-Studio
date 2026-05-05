using CourseInventory.Web.Models;
using CourseInventory.Web.Models.Inventory;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryField> InventoryFields => Set<InventoryField>();
    public DbSet<CustomIdElement> CustomIdElements => Set<CustomIdElement>();
    public DbSet<InventoryAccess> InventoryAccesses => Set<InventoryAccess>();
    public DbSet<DiscussionMessage> DiscussionMessages => Set<DiscussionMessage>();
    public DbSet<ItemLike> ItemLikes => Set<ItemLike>();
    public DbSet<UserLoginActivity> UserLoginActivities => Set<UserLoginActivity>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<InventoryTag> InventoryTags => Set<InventoryTag>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .HasMany(u => u.OwnedInventories)
            .WithOne(i => i.Owner)
            .HasForeignKey(i => i.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Inventory>()
            .Property(i => i.RowVersion)
            .IsRowVersion();

        builder.Entity<Inventory>()
            .HasIndex(i => new { i.OwnerId, i.UpdatedAt });

        builder.Entity<InventoryItem>()
            .Property(i => i.RowVersion)
            .IsRowVersion();

        builder.Entity<InventoryItem>()
            .HasIndex(i => new { i.InventoryId, i.CustomId })
            .IsUnique();

        builder.Entity<InventoryAccess>()
            .HasIndex(a => new { a.InventoryId, a.UserId })
            .IsUnique();

        builder.Entity<ItemLike>()
            .HasIndex(l => new { l.ItemId, l.UserId })
            .IsUnique();

        builder.Entity<UserLoginActivity>()
            .HasIndex(a => new { a.UserId, a.LoggedInAt });

        builder.Entity<Tag>()
            .HasIndex(t => t.NormalizedName)
            .IsUnique();

        builder.Entity<InventoryTag>()
            .HasKey(t => new { t.InventoryId, t.TagId });

        builder.Entity<InventoryTag>()
            .HasOne(t => t.Inventory)
            .WithMany(i => i.InventoryTags)
            .HasForeignKey(t => t.InventoryId);

        builder.Entity<InventoryTag>()
            .HasOne(t => t.Tag)
            .WithMany(t => t.InventoryTags)
            .HasForeignKey(t => t.TagId);

        builder.Entity<UserLoginActivity>()
            .HasOne(a => a.User)
            .WithMany(u => u.LoginActivities)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
