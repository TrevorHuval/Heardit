using Heardit.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heardit.Areas.Identity.Data;

public class HearditDbContext : IdentityDbContext<HearditUser>
{
    public HearditDbContext(DbContextOptions<HearditDbContext> options)
        : base(options)
    {
    }

    public DbSet<Follows> Follows { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ReviewLike> ReviewLikes { get; set; } = default!;
    public DbSet<Song> Songs { get; set; } = default!;


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Follows>()
                .HasKey(f => new { f.UserId, f.FollowerId });
        // Cascade on both sides so deleting an account takes its follow rows with it. Postgres has no
        // objection to the two cascade paths into Follows; that restriction was SQL Server's.
        builder.Entity<Follows>()
            .HasOne(f => f.User)
            .WithMany(u => u.Followers)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Follows>()
            .HasOne(f => f.Follower)
            .WithMany(u => u.Following)
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        // A review always has an author, and dies with them — the views dereference Review.User freely.
        builder.Entity<Review>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        // The song page and the batched feed stats both filter on SongId; every listing orders by CreatedAt.
        builder.Entity<Review>().HasIndex(r => r.SongId);
        builder.Entity<Review>().HasIndex(r => r.CreatedAt);

        // A like belongs to one review by one user; the pair is the key, so the database refuses a
        // double like on its own. It outlives neither the review nor the account that made it.
        builder.Entity<ReviewLike>()
            .HasKey(l => new { l.ReviewId, l.UserId });
        builder.Entity<ReviewLike>()
            .HasOne(l => l.Review)
            .WithMany()
            .HasForeignKey(l => l.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ReviewLike>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Customize the ASP.NET Identity model and override the defaults if needed.
        // For example, you can rename the ASP.NET Identity table names and more.
        // Add your customizations after calling base.OnModelCreating(builder);

        builder.ApplyConfiguration(new ApplicationBuilderUserEntityConfiguration());
    }
}

internal class ApplicationBuilderUserEntityConfiguration : IEntityTypeConfiguration<HearditUser>
{
    public void Configure(EntityTypeBuilder<HearditUser> builder)
    {
        builder.Property(u => u.UserName).HasMaxLength(255);
    }
}