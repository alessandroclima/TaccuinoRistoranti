using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaccuinoRistoranti.Api.Models;

namespace TaccuinoRistoranti.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Meal>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Street).HasMaxLength(200).IsRequired();
            entity.Property(x => x.City).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.ShareCode).HasMaxLength(12).IsRequired();
            entity.HasIndex(x => x.ShareCode).IsUnique();

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Rating>(entity =>
        {
            entity.HasIndex(x => new { x.MealId, x.UserId }).IsUnique();

            entity.HasOne(x => x.Meal)
                .WithMany(x => x.Ratings)
                .HasForeignKey(x => x.MealId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Rating_Location", "\"Location\" >= 0 AND \"Location\" <= 10");
                t.HasCheckConstraint("CK_Rating_Menu", "\"Menu\" >= 0 AND \"Menu\" <= 10");
                t.HasCheckConstraint("CK_Rating_Service", "\"Service\" >= 0 AND \"Service\" <= 10");
                t.HasCheckConstraint("CK_Rating_Bill", "\"Bill\" >= 0 AND \"Bill\" <= 10");
                t.HasCheckConstraint("CK_Rating_Special", "\"Special\" >= 0 AND \"Special\" <= 10");
            });
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.Property(x => x.Token).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RevokedReason).HasMaxLength(200);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
            entity.HasIndex(x => x.ReplacedByTokenId);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
