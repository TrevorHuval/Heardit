using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Heardit.Data;
using System;
using System.Linq;

namespace Heardit.Models
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new HearditContext(
                serviceProvider.GetRequiredService<
                    DbContextOptions<HearditContext>>()))
            {
                context.Database.EnsureCreated();
                // Look for any songs.
                if (context.Song.Any())
                {
                    return;   // DB has been seeded
                }
                context.Song.AddRange(
                    new Song
                    {
                        Title = "Shove It",
                        Artist = "Deftones",
                        Album = "Around the Fur"
                    },
                    new Song
                    {
                        Title = "Man in the Box",
                        Artist = "Alice in Chains",
                        Album = "Facelift"
                    },
                    new Song
                    {
                        Title = "Chop Suey!",
                        Artist = "System of a Down",
                        Album = "Toxicity"
                    },
                    new Song
                    {
                        Title = "King For A Day",
                        Artist = "Pierce the Veil",
                        Album = "Collide With The Sky"
                    }
                );
                context.SaveChanges();
            }
        }
    }
}
