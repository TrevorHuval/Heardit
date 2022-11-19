using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Heardit.Models;

namespace Heardit.Data
{
    public class HearditContext : DbContext
    {
        public HearditContext (DbContextOptions<HearditContext> options)
            : base(options)
        {
        }

        public DbSet<Heardit.Models.Song> Song { get; set; } = default!;
    }
}
