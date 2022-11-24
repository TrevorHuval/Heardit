using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Heardit.Areas.Identity.Data;

namespace Heardit.Models
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new HearditDbContext(
                serviceProvider.GetRequiredService<
                    DbContextOptions<HearditDbContext>>()))
            {
                context.Database.EnsureCreated();
                // Look for any songs.
                if (context.Songs.Any())
                {
                    return;   // DB has been seeded
                }
                context.Songs.AddRange(
                    new Song
                    {
                        Id = "3HfEgAaf0koxBpBB8NvGda",
                        Title = "When You Sleep",
                        Artist = "my bloody valentine",
                        Album = "Loveless",
                        AlbumArt = "https://i.scdn.co/image/ab67616d0000b2730ede770070357575bc050511"
                    }
                );
                context.SaveChanges();
            }
        }
    }
}
