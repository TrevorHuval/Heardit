using Heardit.Areas.Identity.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Heardit.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Lands the links from both confirmation emails: the one sent at sign-up (userId + code) and the one
    /// sent when changing address (userId + email + code). Open to anyone, since the link may be opened
    /// on a phone that isn't signed in.
    /// </summary>
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<HearditUser> _userManager;
        private readonly SignInManager<HearditUser> _signInManager;

        public ConfirmEmailModel(UserManager<HearditUser> userManager, SignInManager<HearditUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public bool Succeeded { get; private set; }

        public bool WasChange { get; private set; }

        public string? Email { get; private set; }

        public async Task<IActionResult> OnGetAsync(string? userId, string? code, string? email = null)
        {
            WasChange = !string.IsNullOrWhiteSpace(email);
            var token = AccountTokens.Decode(code);
            var user = string.IsNullOrWhiteSpace(userId) ? null : await _userManager.FindByIdAsync(userId);
            if (user == null || token == null)
            {
                return Page();
            }

            IdentityResult result;
            if (WasChange)
            {
                // Sets the new address and marks it confirmed in one step; the username is untouched.
                result = await _userManager.ChangeEmailAsync(user, email!, token);
            }
            else if (user.EmailConfirmed)
            {
                // A second click on an old link: nothing to do, and nothing went wrong.
                result = IdentityResult.Success;
            }
            else
            {
                result = await _userManager.ConfirmEmailAsync(user, token);
            }

            Succeeded = result.Succeeded;
            Email = user.Email;

            // Whoever's signed in as this account picks up the change straight away (can post, new email).
            if (Succeeded && _userManager.GetUserId(User) == user.Id)
            {
                await _signInManager.RefreshSignInAsync(user);
            }

            return Page();
        }
    }
}
