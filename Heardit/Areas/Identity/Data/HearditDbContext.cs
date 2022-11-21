using Heardit.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
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

    public DbSet<Heardit.Models.Song> Song { get; set; } = default!;


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
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