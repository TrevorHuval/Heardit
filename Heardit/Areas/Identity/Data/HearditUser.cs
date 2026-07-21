using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

    /// <summary>A line or two the listener writes about themselves. Null until they bother.</summary>
    [MaxLength(BioMaxLength)]
    public string? Bio { get; set; }

    public const int BioMaxLength = 160;

    public ICollection<Follows> Followers { get; set; } = new List<Follows>();
    public ICollection<Follows> Following { get; set; } = new List<Follows>();
}

