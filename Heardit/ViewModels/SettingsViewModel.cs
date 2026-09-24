using Heardit.Areas.Identity.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Heardit.ViewModels
{
    public class SettingsViewModel
    {
        public required HearditUser User { get; init; }

        public bool HasPassword { get; init; }

        /// <summary>Outside logins (Google) connected to this account.</summary>
        public IReadOnlyList<UserLoginInfo> Logins { get; init; } = Array.Empty<UserLoginInfo>();

        /// <summary>Configured providers not yet connected, offered as "Connect".</summary>
        public IReadOnlyList<AuthenticationScheme> AvailableProviders { get; init; } = Array.Empty<AuthenticationScheme>();

        /// <summary>A login can only be removed if something else can still get you in.</summary>
        public bool CanRemoveLogin => HasPassword || Logins.Count > 1;
    }
}
