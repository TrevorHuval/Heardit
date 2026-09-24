using System.ComponentModel.DataAnnotations;
using Heardit.Areas.Identity.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Heardit.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<HearditUser> _userManager;

        public ResetPasswordModel(UserManager<HearditUser> userManager) => _userManager = userManager;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Code { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Choose a new password.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        /// <summary>True once the password has been changed.</summary>
        public bool Done { get; private set; }

        /// <summary>True when the link itself is unusable (missing, mangled or expired).</summary>
        public bool BadLink { get; private set; }

        public IActionResult OnGet(string? email = null, string? code = null)
        {
            if (string.IsNullOrWhiteSpace(email) || AccountTokens.Decode(code) == null)
            {
                BadLink = true;
                return Page();
            }

            Email = email;
            Code = code!;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var token = AccountTokens.Decode(Code);
            HearditUser? user = null;
            try
            {
                user = await _userManager.FindByEmailAsync(Email);
            }
            catch (InvalidOperationException)
            {
            }

            if (token == null || user == null)
            {
                BadLink = true;
                return Page();
            }

            var result = await _userManager.ResetPasswordAsync(user, token, Password);
            if (result.Succeeded)
            {
                // A reset proves control of the inbox, so it also clears any lockout from guessing.
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _userManager.ResetAccessFailedCountAsync(user);
                Done = true;
                return Page();
            }

            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                BadLink = true;
                return Page();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(nameof(Password), error.Description);
            }

            return Page();
        }
    }
}
