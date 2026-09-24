using System.Security.Claims;
using Heardit.Areas.Identity.Data;

namespace Heardit.ViewModels
{
    /// <summary>A profile photo, or the initials tile standing in for one. Size is an .avatar-- modifier.</summary>
    public class AvatarViewModel
    {
        public AvatarViewModel(string? name, string size = "sm", string? userId = null, string? version = null)
        {
            Name = name;
            Size = size;
            UserId = userId;
            Version = version;
        }

        public static AvatarViewModel For(HearditUser user, string size = "sm") =>
            new(user.UserName, size, user.Id, user.AvatarVersion);

        /// <summary>The signed-in user, from the auth cookie, without loading their row.</summary>
        public static AvatarViewModel ForSignedIn(ClaimsPrincipal principal, string? userName, string size = "xs") =>
            new(userName, size, principal.GetLoggedInUserId<string>(), principal.AvatarVersionOf());

        public string? Name { get; }

        public string Size { get; }

        public string? UserId { get; }

        /// <summary>Present only when there's a photo to show.</summary>
        public string? Version { get; }

        public bool HasPhoto => !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(Version);

        public string Initials
        {
            get
            {
                var name = (Name ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    return "?";
                }

                var parts = name.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
                }

                return name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
            }
        }
    }
}
