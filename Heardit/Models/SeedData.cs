using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Heardit.Areas.Identity.Data;

namespace Heardit.Models
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var context = new HearditDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<HearditDbContext>>());

            await context.Database.MigrateAsync();

            // Look for any songs.
            if (await context.Songs.AnyAsync())
            {
                return;   // DB has been seeded
            }

            context.Songs.Add(
                new Song
                {
                    Id = "3HfEgAaf0koxBpBB8NvGda",
                    Title = "When You Sleep",
                    Artist = "my bloody valentine",
                    Album = "Loveless",
                    AlbumArt = "https://i.scdn.co/image/ab67616d0000b2730ede770070357575bc050511"
                }
            );

            await context.SaveChangesAsync();
        }
    }
}
