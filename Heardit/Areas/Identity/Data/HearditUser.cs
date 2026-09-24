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

    /// <summary>
    /// Whether this account has to confirm its email before it can post (review, like, follow).
    /// True for accounts created after verification shipped; accounts from before stay grandfathered.
    /// </summary>
    public bool MustVerifyEmail { get; set; }

    /// <summary>
    /// Changes whenever the profile photo does; null when there isn't one. Carried into the auth cookie
    /// and the photo URL, so pages can show the photo without loading it and browsers can cache it forever.
    /// </summary>
    [MaxLength(32)]
    public string? AvatarVersion { get; set; }

    /// <summary>Whether this account is allowed to post, given its verification state.</summary>
    public bool CanPost => EmailConfirmed || !MustVerifyEmail;

    public ICollection<Follows> Followers { get; set; } = new List<Follows>();
    public ICollection<Follows> Following { get; set; } = new List<Follows>();
}

