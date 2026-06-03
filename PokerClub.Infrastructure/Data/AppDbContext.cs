using Microsoft.EntityFrameworkCore;
using PokerClub.Domain.Entities;

namespace PokerClub.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<City> Cities => Set<City>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Таблица Cities ---
        modelBuilder.Entity<City>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(100).IsRequired();
        });

        // --- Таблица Clubs ---
        modelBuilder.Entity<Club>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(300);
            
            entity.HasOne(e => e.City)
                  .WithMany(c => c.Clubs)
                  .HasForeignKey(e => e.CityId)
                  .OnDelete(DeleteBehavior.Restrict); // Нельзя удалить город, если там есть клубы
        });

        // --- Таблица Tournaments ---
        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Format).HasMaxLength(50);
            entity.Property(e => e.BuyIn).HasColumnType("numeric(18,2)"); // Формат денег для Postgre

            entity.HasOne(e => e.Club)
                  .WithMany(c => c.Tournaments)
                  .HasForeignKey(e => e.ClubId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // --- Таблица Users ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.VkId).IsUnique(); // Строго по ТЗ
            entity.Property(e => e.VkId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
        });

        // --- Таблица Registrations ---
        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Композитный уникальный ключ: 1 юзер = 1 рега на конкретный турнир
            entity.HasIndex(e => new { e.TournamentId, e.UserId }).IsUnique();

            entity.HasOne(e => e.Tournament)
                  .WithMany(t => t.Registrations)
                  .HasForeignKey(e => e.TournamentId)
                  .OnDelete(DeleteBehavior.Cascade); // Удалили турик -> удалились реги

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Registrations)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade); // Удалили юзера -> удалились реги
        });
    }
}