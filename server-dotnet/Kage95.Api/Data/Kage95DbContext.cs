using System.Text.Json;
using Kage95.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kage95.Api.Data;

public class Kage95DbContext(DbContextOptions<Kage95DbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Manga> Mangas => Set<Manga>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ReadingListEntry> ReadingLists => Set<ReadingListEntry>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<ChapterPage> ChapterPages => Set<ChapterPage>();
    public DbSet<PointRecord> PointRecords => Set<PointRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var genresConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => string.IsNullOrWhiteSpace(v) ? new List<string>() : (JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>())
        );
        var genresComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("int unsigned").ValueGeneratedOnAdd();
            e.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(60).IsRequired();
            e.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(60).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(24).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(160).IsRequired();
            e.Property(x => x.Role).HasColumnName("role").HasMaxLength(20).HasDefaultValue(UserRoles.User).IsRequired();
            e.Property(x => x.Bio).HasColumnName("bio").HasMaxLength(300);
            e.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<Session>(e =>
        {
            e.ToTable("sessions");
            e.HasKey(x => x.TokenHash);
            e.Property(x => x.TokenHash).HasColumnName("token_hash").HasColumnType("binary(32)");
            e.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("int unsigned");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("datetime");
            e.HasIndex(x => x.ExpiresAt);
            e.HasOne(x => x.User).WithMany(u => u.Sessions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Manga>(e =>
        {
            e.ToTable("mangas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("int unsigned").ValueGeneratedOnAdd();
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(150).IsRequired();
            e.Property(x => x.JapaneseTitle).HasColumnName("japanese_title").HasMaxLength(150).IsRequired();
            e.Property(x => x.Author).HasColumnName("author").HasMaxLength(150).IsRequired();
            e.Property(x => x.Genres).HasColumnName("genres").HasColumnType("json")
                .HasConversion(genresConverter, genresComparer).IsRequired();
            e.Property(x => x.Summary).HasColumnName("summary").HasColumnType("text").IsRequired();
            e.Property(x => x.Cover).HasColumnName("cover").HasMaxLength(255).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
            e.Property(x => x.CatalogRating).HasColumnName("catalog_rating").HasColumnType("decimal(3,1)");
            e.Property(x => x.Chapters).HasColumnName("chapters").HasColumnType("int unsigned");
            e.Property(x => x.Year).HasColumnName("year").HasColumnType("smallint unsigned");
            e.Property(x => x.Views).HasColumnName("views").HasMaxLength(24).IsRequired();
            e.Property(x => x.Accent).HasColumnName("accent").HasColumnType("char(7)").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Favorite>(e =>
        {
            e.ToTable("favorites");
            e.HasKey(x => new { x.UserId, x.MangaId });
            e.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("int unsigned");
            e.Property(x => x.MangaId).HasColumnName("manga_id").HasColumnType("int unsigned");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.User).WithMany(u => u.Favorites).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Manga).WithMany(m => m.Favorites).HasForeignKey(x => x.MangaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Rating>(e =>
        {
            e.ToTable("ratings");
            e.HasKey(x => new { x.MangaId, x.UserId });
            e.Property(x => x.MangaId).HasColumnName("manga_id").HasColumnType("int unsigned");
            e.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("int unsigned");
            e.Property(x => x.Score).HasColumnName("score").HasColumnType("tinyint unsigned");
            e.HasOne(x => x.User).WithMany(u => u.Ratings).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Manga).WithMany(m => m.Ratings).HasForeignKey(x => x.MangaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>(e =>
        {
            e.ToTable("comments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("int unsigned").ValueGeneratedOnAdd();
            e.Property(x => x.MangaId).HasColumnName("manga_id").HasColumnType("int unsigned");
            e.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("int unsigned");
            e.Property(x => x.Body).HasColumnName("body").HasMaxLength(1000).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.MangaId, x.Id });
            e.HasOne(x => x.User).WithMany(u => u.Comments).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Manga).WithMany(m => m.Comments).HasForeignKey(x => x.MangaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Friendship>(e =>
        {
            e.ToTable("friendships");
            e.HasKey(x => new { x.UserLow, x.UserHigh });
            e.Property(x => x.UserLow).HasColumnName("user_low").HasColumnType("int unsigned");
            e.Property(x => x.UserHigh).HasColumnName("user_high").HasColumnType("int unsigned");
            e.Property(x => x.RequestedBy).HasColumnName("requested_by").HasColumnType("int unsigned");
            e.Property(x => x.Status).HasColumnName("status")
                .HasColumnType("enum('pending','accepted','blocked')")
                .HasDefaultValue(FriendshipStatuses.Pending)
                .IsRequired();
            e.Property(x => x.BlockedBy).HasColumnName("blocked_by").HasColumnType("int unsigned");
            e.HasIndex(x => new { x.UserHigh, x.UserLow });
            e.HasOne(x => x.LowUser).WithMany().HasForeignKey(x => x.UserLow).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.HighUser).WithMany().HasForeignKey(x => x.UserHigh).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("int unsigned").ValueGeneratedOnAdd();
            e.Property(x => x.UserLow).HasColumnName("user_low").HasColumnType("int unsigned");
            e.Property(x => x.UserHigh).HasColumnName("user_high").HasColumnType("int unsigned");
            e.Property(x => x.SenderId).HasColumnName("sender_id").HasColumnType("int unsigned");
            e.Property(x => x.Body).HasColumnName("body").HasMaxLength(2000);
            e.Property(x => x.MangaId).HasColumnName("manga_id").HasColumnType("int unsigned");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.UserLow, x.UserHigh, x.Id });
            e.HasOne(x => x.Friendship).WithMany(f => f.Messages)
                .HasForeignKey(x => new { x.UserLow, x.UserHigh })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Manga).WithMany().HasForeignKey(x => x.MangaId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ReadingListEntry>(e =>
        {
            e.ToTable("reading_lists");
            e.HasKey(x => new { x.UserId, x.MangaId });
            e.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("int unsigned");
            e.Property(x => x.MangaId).HasColumnName("manga_id").HasColumnType("int unsigned");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue(ReadingStatuses.Reading).IsRequired();
            e.Property(x => x.CurrentChapter).HasColumnName("current_chapter").HasColumnType("int unsigned").HasDefaultValue(0u);
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.User).WithMany(u => u.ReadingList).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Manga).WithMany(m => m.ReadingListEntries).HasForeignKey(x => x.MangaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Chapter>(e =>
        {
            e.ToTable("chapters");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("int unsigned").ValueGeneratedOnAdd();
            e.Property(x => x.MangaId).HasColumnName("manga_id").HasColumnType("int unsigned");
            e.Property(x => x.ChapterNumber).HasColumnName("chapter_number").HasColumnType("decimal(6,1)");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(150).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.MangaId, x.ChapterNumber }).IsUnique();
            e.HasOne(x => x.Manga).WithMany(m => m.ChapterItems).HasForeignKey(x => x.MangaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChapterPage>(e =>
        {
            e.ToTable("chapter_pages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("int unsigned").ValueGeneratedOnAdd();
            e.Property(x => x.ChapterId).HasColumnName("chapter_id").HasColumnType("int unsigned");
            e.Property(x => x.PageNumber).HasColumnName("page_number").HasColumnType("int unsigned");
            e.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(500).IsRequired();
            e.HasIndex(x => new { x.ChapterId, x.PageNumber }).IsUnique();
            e.HasOne(x => x.Chapter).WithMany(c => c.Pages).HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PointRecord>(e =>
        {
            e.ToTable("point_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("int unsigned").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("int unsigned");
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(32).IsRequired();
            e.Property(x => x.Points).HasColumnName("points");
            e.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(80);
            e.Property(x => x.Note).HasColumnName("note").HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.UserId, x.Action, x.Reference });
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.User).WithMany(u => u.PointRecords).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
