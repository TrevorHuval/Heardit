using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Heardit.Areas.Identity.Data
{
    /// <summary>
    /// Account facts every page needs, carried in the auth cookie so no page has to load the user for
    /// them. Refreshed on sign-in, after the account changes (RefreshSignInAsync), and whenever Identity
    /// revalidates the security stamp.
    /// </summary>
    public static class HearditClaims
    {
        public const string CanPost = "heardit:can_post";
        public const string AvatarVersion = "heardit:avatar";

        /// <summary>
        /// Whether the signed-in user may review, like and follow. A cookie issued before this claim
        /// existed has no value at all; those sessions belong to grandfathered accounts, so treat them as allowed.
        /// </summary>
        public static bool CanPostReviews(this ClaimsPrincipal principal) =>
            principal.FindFirstValue(CanPost) != "false";

        public static string? AvatarVersionOf(this ClaimsPrincipal principal) =>
            principal.FindFirstValue(AvatarVersion);
    }

    public class HearditClaimsPrincipalFactory : UserClaimsPrincipalFactory<HearditUser, IdentityRole>
    {
        public HearditClaimsPrincipalFactory(
            UserManager<HearditUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options)
            : base(userManager, roleManager, options)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(HearditUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            identity.AddClaim(new Claim(HearditClaims.CanPost, user.CanPost ? "true" : "false"));
            if (!string.IsNullOrEmpty(user.AvatarVersion))
            {
                identity.AddClaim(new Claim(HearditClaims.AvatarVersion, user.AvatarVersion));
            }

            return identity;
        }
    }
}
