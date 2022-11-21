using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Reflection.Emit;
using static System.Reflection.Metadata.BlobBuilder;

namespace Heardit.Areas.Identity.Data;

public class HearditDbContext : IdentityDbContext<HearditUser>
{
    public HearditDbContext(DbContextOptions<HearditDbContext> options)
        : base(options)
    {
    }

    public DbSet<Heardit.Models.Song> Song { get; set; } = default!;
    public DbSet<Follows> Follows { get; set; }
    public DbSet<Review> Reviews { get; set; }


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Follows>()
                .HasKey(f => new { f.UserId, f.FollowerId });
        builder.Entity<Follows>()
            .HasOne(f => f.User)
            .WithMany(u => u.Followers)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.Entity<Follows>()
            .HasOne(f => f.Follower)
            .WithMany(u => u.Following)
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.NoAction);

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
        builder.Property(u => u.DisplayName).HasMaxLength(255);
    }
}