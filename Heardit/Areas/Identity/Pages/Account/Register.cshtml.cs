using System.ComponentModel.DataAnnotations;
using Heardit.Areas.Identity.Data;
using Heardit.Services;
using Heardit.Services.Email;
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
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<HearditUser> _signInManager;
        private readonly UserManager<HearditUser> _userManager;
        private readonly IAccountEmails _emails;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<HearditUser> userManager,
            SignInManager<HearditUser> signInManager,
            IAccountEmails emails,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emails = emails;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public ExternalLoginsViewModel Providers { get; private set; } = new(Array.Empty<AuthenticationScheme>(), null);

        public string? ReturnUrl { get; private set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Pick a username.")]
            [StringLength(UserNameRules.MaxLength, MinimumLength = UserNameRules.MinLength, ErrorMessage = UserNameRules.Hint)]
            [RegularExpression(UserNameRules.Pattern, ErrorMessage = UserNameRules.Hint)]
            [Display(Name = "Username")]
            public string UserName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Enter your email.")]
            [EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
            [StringLength(254)]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Choose a password.")]
            [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(Url.Content("~/"));
            }

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

            var user = new HearditUser
            {
                UserName = Input.UserName.Trim(),
                Email = Input.Email.Trim(),
                // New accounts confirm their email before they can post; see RequireVerifiedEmail.
                MustVerifyEmail = true
            };

            var result = await _userManager.CreateAsync(user, Input.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(FieldFor(error.Code), Friendly(error));
                }

                return Page();
            }

            _logger.LogInformation("User created a new account with password.");
            await _signInManager.SignInAsync(user, isPersistent: true);

            var sent = await SendVerificationAsync(user);
            TempData["Flash"] = sent
                ? $"Welcome to Heardit! We sent a link to {user.Email}. Confirm it to start reviewing."
                : "Welcome to Heardit! We couldn't send your confirmation email just now. You can resend it from Settings.";

            var home = Url.Content("~/");
            return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : home);
        }

        private async Task<bool> SendVerificationAsync(HearditUser user)
        {
            var code = AccountTokens.Encode(await _userManager.GenerateEmailConfirmationTokenAsync(user));
            var link = Url.Page("/Account/ConfirmEmail", null, new { area = "Identity", userId = user.Id, code }, Request.Scheme)!;
            return await _emails.SendVerificationAsync(user.Email!, user.UserName!, link);
        }

        private async Task LoadAsync(string? returnUrl)
        {
            ReturnUrl = returnUrl;
            Providers = new ExternalLoginsViewModel(
                (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList(), returnUrl);
        }

        /// <summary>Put Identity's errors next to the field they're about.</summary>
        private static string FieldFor(string code) => code switch
        {
            nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.InvalidUserName) => "Input.UserName",
            nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.InvalidEmail) => "Input.Email",
            _ when code.StartsWith("Password", StringComparison.Ordinal) => "Input.Password",
            _ => string.Empty
        };

        private static string Friendly(IdentityError error) => error.Code switch
        {
            nameof(IdentityErrorDescriber.DuplicateUserName) => "That username is taken.",
            nameof(IdentityErrorDescriber.DuplicateEmail) => "An account already uses that email. Log in instead, or reset your password.",
            nameof(IdentityErrorDescriber.InvalidUserName) => UserNameRules.Hint,
            _ => error.Description
        };
    }
}
