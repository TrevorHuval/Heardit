using System.ComponentModel.DataAnnotations;
using Heardit.Areas.Identity.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Heardit.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<HearditUser> _signInManager;
        private readonly UserManager<HearditUser> _userManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<HearditUser> signInManager, UserManager<HearditUser> userManager, ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public ExternalLoginsViewModel Providers { get; private set; } = new(Array.Empty<AuthenticationScheme>(), null);

        public string? ReturnUrl { get; private set; }

        /// <summary>Set by the Google flow when it sends someone back here with a problem.</summary>
        [TempData(Key = "LoginError")]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Enter your username or email.")]
            [Display(Name = "Username or email")]
            public string Login { get; set; } = string.Empty;

            [Required(ErrorMessage = "Enter your password.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Keep me signed in")]
            public bool RememberMe { get; set; } = true;
        }

        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(SafeReturnUrl(returnUrl));
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            // Start clean: a half-finished Google sign-in must not leak into this one.
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            await LoadAsync(returnUrl);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            await LoadAsync(returnUrl);
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await FindUserAsync(Input.Login.Trim());
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "That username or email and password don't match.");
                return Page();
            }

            // Failures count toward a short lockout to slow down password guessing.
            var result = await _signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");
                return LocalRedirect(SafeReturnUrl(returnUrl));
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, await _userManager.HasPasswordAsync(user)
                ? "That username or email and password don't match."
                : "This account signs in with Google. Use the button above.");
            return Page();
        }

        /// <summary>By username, or by email when it looks like one. Usernames can't contain '@'.</summary>
        private async Task<HearditUser?> FindUserAsync(string login)
        {
            if (!login.Contains('@'))
            {
                return await _userManager.FindByNameAsync(login);
            }

            try
            {
                return await _userManager.FindByEmailAsync(login);
            }
            catch (InvalidOperationException)
            {
                // Accounts from before emails had to be unique can share one; the username still works.
                return null;
            }
        }

        private async Task LoadAsync(string? returnUrl)
        {
            ReturnUrl = returnUrl;
            Providers = new ExternalLoginsViewModel(
                (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList(), returnUrl);
        }

        private string SafeReturnUrl(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
    }
}
