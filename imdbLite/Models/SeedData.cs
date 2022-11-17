using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using imdbLite.Data;
using System;
using System.Linq;

namespace imdbLite.Models
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new imdbLiteContext(
                serviceProvider.GetRequiredService<
                    DbContextOptions<imdbLiteContext>>()))
            {
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
