using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace Heardit.Areas.Identity.Data;

// Add profile data for application users by adding properties to the HearditUser class
public class HearditUser : IdentityUser
{
     public string DisplayName { get; set; }
}

