using Microsoft.EntityFrameworkCore;
using Mithara.Web.Models;
using Mithara.Web.Models.Forum;

namespace Mithara.Web.Data;

public class WebDbContext : DbContext
{
    public WebDbContext(DbContextOptions<WebDbContext> options) : base(options) { }

    public DbSet<ForumCategory> ForumCategories => Set<ForumCategory>();
    public DbSet<ForumTopic> ForumTopics => Set<ForumTopic>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<AdminLog> AdminLogs => Set<AdminLog>();
    public DbSet<SiteConfig> SiteConfigs => Set<SiteConfig>();
    public DbSet<ShopProduct> ShopProducts => Set<ShopProduct>();
    public DbSet<ShopOrder> ShopOrders => Set<ShopOrder>();
    public DbSet<WikiMob> WikiMobs => Set<WikiMob>();
    public DbSet<WikiDrop> WikiDrops => Set<WikiDrop>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ForumCategory>(entity =>
        {
            entity.ToTable("forum_categories");
            entity.HasIndex(e => e.SortOrder);
        });

        modelBuilder.Entity<ForumTopic>(entity =>
        {
            entity.ToTable("forum_topics");
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Topics)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ForumPost>(entity =>
        {
            entity.ToTable("forum_posts");
            entity.HasIndex(e => e.TopicId);
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne(e => e.Topic)
                  .WithMany(t => t.Posts)
                  .HasForeignKey(e => e.TopicId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminLog>(entity =>
        {
            entity.ToTable("admin_logs");
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<SiteConfig>(entity =>
        {
            entity.ToTable("site_config");
        });

        modelBuilder.Entity<ShopProduct>(entity =>
        {
            entity.ToTable("shop_products");
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<ShopOrder>(entity =>
        {
            entity.ToTable("shop_orders");
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<WikiMob>(entity =>
        {
            entity.ToTable("wiki_mobs");
            entity.HasIndex(e => e.PrefabId);
            entity.HasIndex(e => e.Level);
        });

        modelBuilder.Entity<WikiDrop>(entity =>
        {
            entity.ToTable("wiki_drops");
            entity.HasIndex(e => e.MobId);
            entity.HasIndex(e => e.ItemId);
        });
    }

    public async Task EnsureTablesCreatedAsync()
    {
        await Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS forum_categories (
                "Id" SERIAL PRIMARY KEY,
                "Name" VARCHAR(100) NOT NULL,
                "Description" VARCHAR(500) NOT NULL DEFAULT '',
                "SortOrder" INT NOT NULL DEFAULT 0,
                "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
                "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS forum_topics (
                "Id" SERIAL PRIMARY KEY,
                "CategoryId" INT NOT NULL,
                "Title" VARCHAR(200) NOT NULL,
                "AccountId" INT NOT NULL,
                "AuthorName" VARCHAR(50) NOT NULL DEFAULT '',
                "IsPinned" BOOLEAN NOT NULL DEFAULT FALSE,
                "IsLocked" BOOLEAN NOT NULL DEFAULT FALSE,
                "ViewCount" INT NOT NULL DEFAULT 0,
                "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "LastPostAt" TIMESTAMP,
                FOREIGN KEY ("CategoryId") REFERENCES forum_categories("Id") ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS forum_posts (
                "Id" SERIAL PRIMARY KEY,
                "TopicId" INT NOT NULL,
                "AccountId" INT NOT NULL,
                "AuthorName" VARCHAR(50) NOT NULL DEFAULT '',
                "Content" TEXT NOT NULL,
                "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "EditedAt" TIMESTAMP,
                FOREIGN KEY ("TopicId") REFERENCES forum_topics("Id") ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS admin_logs (
                "Id" SERIAL PRIMARY KEY,
                "AccountId" INT NOT NULL,
                "Username" VARCHAR(50) NOT NULL DEFAULT '',
                "Action" VARCHAR(50) NOT NULL DEFAULT '',
                "Details" TEXT NOT NULL DEFAULT '',
                "IpAddress" VARCHAR(50) NOT NULL DEFAULT '',
                "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS site_config (
                "Key" VARCHAR(100) PRIMARY KEY,
                "Value" TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS shop_products (
                "Id" SERIAL PRIMARY KEY,
                "Name" VARCHAR(200) NOT NULL,
                "Description" TEXT NOT NULL DEFAULT '',
                "Category" VARCHAR(50) NOT NULL DEFAULT '',
                "Price" DECIMAL(10,2) NOT NULL DEFAULT 0,
                "CashAmount" INT NOT NULL DEFAULT 0,
                "Tier" VARCHAR(50) NOT NULL DEFAULT '',
                "IconClass" VARCHAR(100) NOT NULL DEFAULT '',
                "BadgeText" VARCHAR(100) NOT NULL DEFAULT '',
                "SortOrder" INT NOT NULL DEFAULT 0,
                "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
                "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS shop_orders (
                "Id" SERIAL PRIMARY KEY,
                "AccountId" INT NOT NULL,
                "Username" VARCHAR(50) NOT NULL DEFAULT '',
                "ProductId" INT NOT NULL,
                "ProductName" VARCHAR(200) NOT NULL DEFAULT '',
                "Amount" DECIMAL(10,2) NOT NULL DEFAULT 0,
                "TargetCharacterId" INT,
                "TargetCharacterName" VARCHAR(50),
                "Status" VARCHAR(20) NOT NULL DEFAULT 'pending',
                "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "CompletedAt" TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS wiki_mobs (
                "Id" SERIAL PRIMARY KEY,
                "PrefabId" VARCHAR(100) NOT NULL DEFAULT '',
                "Name" VARCHAR(200) NOT NULL,
                "Level" INT NOT NULL DEFAULT 1,
                "Health" INT NOT NULL DEFAULT 50,
                "AttackDamage" INT NOT NULL DEFAULT 5,
                "ExperienceReward" INT NOT NULL DEFAULT 10,
                "GoldMin" INT NOT NULL DEFAULT 1,
                "GoldMax" INT NOT NULL DEFAULT 10,
                "IsBoss" BOOLEAN NOT NULL DEFAULT FALSE,
                "IsElite" BOOLEAN NOT NULL DEFAULT FALSE,
                "ImageUrl" VARCHAR(500) NOT NULL DEFAULT '',
                "Description" TEXT NOT NULL DEFAULT '',
                "SpawnMaps" VARCHAR(500) NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS wiki_drops (
                "Id" SERIAL PRIMARY KEY,
                "MobId" INT NOT NULL,
                "ItemId" INT NOT NULL,
                "ItemName" VARCHAR(200) NOT NULL DEFAULT '',
                "ItemIcon" VARCHAR(500) NOT NULL DEFAULT '',
                "MinQuantity" INT NOT NULL DEFAULT 1,
                "MaxQuantity" INT NOT NULL DEFAULT 1,
                "DropChance" DOUBLE PRECISION NOT NULL DEFAULT 0,
                "DropChanceDisplay" VARCHAR(20) NOT NULL DEFAULT '',
                "IsEquipmentDrop" BOOLEAN NOT NULL DEFAULT FALSE,
                "EquipmentTier" VARCHAR(20) NOT NULL DEFAULT '',
                FOREIGN KEY ("MobId") REFERENCES wiki_mobs("Id") ON DELETE CASCADE
            );
            """);
    }
}
