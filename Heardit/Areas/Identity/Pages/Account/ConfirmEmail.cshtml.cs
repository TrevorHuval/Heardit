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
    /// on a phone that isn't signed in. The link shows a confirm button; only the button's POST acts.
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

        /// <summary>True on the first visit with a well-formed link: the page asks for one click to confirm.</summary>
        public bool Pending { get; private set; }

        public bool Succeeded { get; private set; }

        public bool WasChange { get; private set; }

        public string? Email { get; private set; }

        public string? UserId { get; private set; }

        public string? Code { get; private set; }

        /// <summary>
        /// Opening the link changes nothing; it shows a button that posts back. Mail scanners and link
        /// previews fetch every URL in a message, and a link that confirmed on GET would let them verify
        /// an address on behalf of whoever typed it in at sign-up, without its owner ever agreeing.
        /// </summary>
        public IActionResult OnGet(string? userId, string? code, string? email = null)
        {
            WasChange = !string.IsNullOrWhiteSpace(email);
            if (string.IsNullOrWhiteSpace(userId) || AccountTokens.Decode(code) == null)
            {
                return Page();
            }

            Pending = true;
            UserId = userId;
            Code = code;
            Email = WasChange ? email : null;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? userId, string? code, string? email = null)
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
