using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using imdbLite.Models;

namespace imdbLite.Data
{
    public class imdbLiteContext : DbContext
    {
        public imdbLiteContext (DbContextOptions<imdbLiteContext> options)
            : base(options)
        {
        }

        public DbSet<imdbLite.Models.Song> Song { get; set; } = default!;
    }
}
