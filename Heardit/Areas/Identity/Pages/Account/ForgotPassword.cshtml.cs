using System.ComponentModel.DataAnnotations;
using Heardit.Areas.Identity.Data;
using Heardit.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Heardit.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<HearditUser> _userManager;
        private readonly IAccountEmails _emails;

        public ForgotPasswordModel(UserManager<HearditUser> userManager, IAccountEmails emails)
        {
            _userManager = userManager;
            _emails = emails;
        }

        [BindProperty]
        [Required(ErrorMessage = "Enter the email on your account.")]
        [EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>After a post: the page shows the same "check your inbox" either way.</summary>
        public bool Submitted { get; private set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            HearditUser? user = null;
            try
            {
                user = await _userManager.FindByEmailAsync(Email.Trim());
            }
            catch (InvalidOperationException)
            {
                // Two old accounts share this address; neither gets a reset link that could open the other.
            }

            // Only to a confirmed address: resetting through an unproven one would hand the account to
            // whoever typed it in at sign-up. Either way the page says the same thing, so it can't be
            // used to find out who has an account.
            if (user != null && user.EmailConfirmed)
            {
                var code = AccountTokens.Encode(await _userManager.GeneratePasswordResetTokenAsync(user));
                var link = Url.Page("/Account/ResetPassword", null, new { area = "Identity", email = user.Email, code }, Request.Scheme)!;
                await _emails.SendPasswordResetAsync(user.Email!, user.UserName!, link);
            }

            Submitted = true;
            return Page();
        }
    }
}
