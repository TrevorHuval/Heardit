using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Heardit.Models;
using Microsoft.AspNetCore.Identity;

namespace Heardit.Areas.Identity.Data;

// Add profile data for application users by adding properties to the HearditUser class
public class HearditUser : IdentityUser
{
    public HearditUser() : base()
    { }

    public HearditUser(string UserName) : base(UserName)
    { }
    //public ICollection<HearditUser> Followers { get; set; }
    //public ICollection<HearditUser> Following { get; set; }
    public ICollection<Follows> Followers { get; set; }
    public ICollection<Follows> Following { get; set; }
}

