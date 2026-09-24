using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
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
    /// <summary>
    /// Sign in with Google: sends the browser to Google, handles the return, and for a first-time Google
    /// user asks for a username before creating the account. Also connects Google to an existing account
    /// from Settings.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<HearditUser> _signInManager;
        private readonly UserManager<HearditUser> _userManager;
        private readonly IAccountEmails _emails;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            SignInManager<HearditUser> signInManager,
            UserManager<HearditUser> userManager,
            IAccountEmails emails,
            ILogger<ExternalLoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emails = emails;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ProviderDisplayName { get; private set; } = "Google";

        /// <summary>The address the provider gave us; when present it isn't editable.</summary>
        public string? ProviderEmail { get; private set; }

        public string? ReturnUrl { get; private set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Pick a username.")]
            [StringLength(UserNameRules.MaxLength, MinimumLength = UserNameRules.MinLength, ErrorMessage = UserNameRules.Hint)]
            [RegularExpression(UserNameRules.Pattern, ErrorMessage = UserNameRules.Hint)]
            [Display(Name = "Username")]
            public string UserName { get; set; } = string.Empty;

            /// <summary>Only asked for when the provider didn't share an email.</summary>
            [EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
            [Display(Name = "Email")]
            public string? Email { get; set; }
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        /// <summary>"Continue with Google" on the log-in and sign-up pages.</summary>
        public IActionResult OnPost(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        /// <summary>"Connect Google" in Settings: the same trip, tied to the signed-in account.</summary>
        public IActionResult OnPostLink(string provider)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return RedirectToPage("./Login");
            }

            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "LinkCallback");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, userId);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError != null)
            {
                return BackToLogin("Google sign-in was cancelled or failed. Try again.", returnUrl);
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return BackToLogin("That sign-in took too long. Try again.", returnUrl);
            }

            // Already connected: just sign in.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in with {Provider}.", info.LoginProvider);
                return LocalRedirect(SafeReturnUrl(returnUrl));
            }

            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var existing = await FindByEmailAsync(email);
            switch (ExternalAccounts.Decide(existing, ExternalAccounts.ProviderVerifiedEmail(info.Principal)))
            {
                case ExternalSignUpOutcome.LinkToExisting:
                    var linked = await _userManager.AddLoginAsync(existing!, info);
                    if (!linked.Succeeded)
                    {
                        return BackToLogin("We couldn't connect Google to your account. Try again.", returnUrl);
                    }

                    await _signInManager.SignInAsync(existing!, isPersistent: true, info.LoginProvider);
                    TempData["Flash"] = $"{info.ProviderDisplayName} is now connected to your account.";
                    return LocalRedirect(SafeReturnUrl(returnUrl));

                case ExternalSignUpOutcome.SignInFirst:
                    return BackToLogin(
                        $"An account already uses {email}. Log in with its password, then connect {info.ProviderDisplayName} from Settings.",
                        returnUrl);

                default:
                    // First time here: choose a username, then the account is created.
                    Show(info, returnUrl);
                    Input.UserName = await AvailableSuggestionAsync(
                        ExternalAccounts.SuggestUserName(info.Principal.FindFirstValue(ClaimTypes.Name), email));
                    return Page();
            }
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string? returnUrl = null)
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return BackToLogin("That sign-in took too long. Try again.", returnUrl);
            }

            Show(info, returnUrl);
            var email = ProviderEmail ?? Input.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("Input.Email", "Enter your email.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Re-check: someone may have registered this address since the callback.
            var existing = await FindByEmailAsync(email);
            if (existing != null)
            {
                return BackToLogin(
                    $"An account already uses {email}. Log in with its password, then connect {info.ProviderDisplayName} from Settings.",
                    returnUrl);
            }

            var providerVerified = ProviderEmail != null && ExternalAccounts.ProviderVerifiedEmail(info.Principal);
            var user = new HearditUser
            {
                UserName = Input.UserName.Trim(),
                Email = email,
                // Google has already proven the address; a typed-in one still needs the usual link.
                EmailConfirmed = providerVerified,
                MustVerifyEmail = true
            };

            var created = await _userManager.CreateAsync(user);
            if (created.Succeeded)
            {
                created = await _userManager.AddLoginAsync(user, info);
            }

            if (!created.Succeeded)
            {
                foreach (var error in created.Errors)
                {
                    ModelState.AddModelError(
                        error.Code == nameof(IdentityErrorDescriber.DuplicateUserName) ? "Input.UserName" : string.Empty,
                        error.Code == nameof(IdentityErrorDescriber.DuplicateUserName) ? "That username is taken." : error.Description);
                }

                if (await _userManager.FindByIdAsync(user.Id) is { } partial && !(await _userManager.GetLoginsAsync(partial)).Any())
                {
                    // Never leave behind an account nobody can sign in to.
                    await _userManager.DeleteAsync(partial);
                }

                return Page();
            }

            _logger.LogInformation("User created an account using {Provider}.", info.LoginProvider);
            await _signInManager.SignInAsync(user, isPersistent: true, info.LoginProvider);

            if (!providerVerified)
            {
                var code = AccountTokens.Encode(await _userManager.GenerateEmailConfirmationTokenAsync(user));
                var link = Url.Page("/Account/ConfirmEmail", null, new { area = "Identity", userId = user.Id, code }, Request.Scheme)!;
                await _emails.SendVerificationAsync(email!, user.UserName!, link);
                TempData["Flash"] = $"Welcome to Heardit! We sent a link to {email}. Confirm it to start reviewing.";
            }
            else
            {
                TempData["Flash"] = "Welcome to Heardit!";
            }

            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        public async Task<IActionResult> OnGetLinkCallbackAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("./Login");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync(user.Id);
            if (info == null)
            {
                TempData["FlashError"] = "Connecting Google didn't finish. Try again.";
                return RedirectToAction("Index", "Settings", new { area = "" });
            }

            var result = await _userManager.AddLoginAsync(user, info);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            if (result.Succeeded)
            {
                TempData["Flash"] = $"{info.ProviderDisplayName} is connected. You can use it to log in.";
            }
            else
            {
                TempData["FlashError"] = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.LoginAlreadyAssociated))
                    ? $"That {info.ProviderDisplayName} account is already connected to a different Heardit account."
                    : $"We couldn't connect {info.ProviderDisplayName}. Try again.";
            }

            return RedirectToAction("Index", "Settings", new { area = "" });
        }

        private void Show(ExternalLoginInfo info, string? returnUrl)
        {
            ReturnUrl = returnUrl;
            ProviderDisplayName = info.ProviderDisplayName ?? info.LoginProvider;
            ProviderEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
        }

        private async Task<HearditUser?> FindByEmailAsync(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            try
            {
                return await _userManager.FindByEmailAsync(email);
            }
            catch (InvalidOperationException)
            {
                // Two old accounts share this address. Treat it as taken and never link to either.
                return new HearditUser { Email = email };
            }
        }

        /// <summary>The suggestion, or the suggestion with a number on the end if it's taken.</summary>
        private async Task<string> AvailableSuggestionAsync(string suggestion)
        {
            if (await _userManager.FindByNameAsync(suggestion) == null)
            {
                return suggestion;
            }

            for (var i = 2; i < 100; i++)
            {
                var suffix = i.ToString();
                var candidate = suggestion.Length + suffix.Length > UserNameRules.MaxLength
                    ? suggestion[..(UserNameRules.MaxLength - suffix.Length)] + suffix
                    : suggestion + suffix;
                if (await _userManager.FindByNameAsync(candidate) == null)
                {
                    return candidate;
                }
            }

            return suggestion;
        }

        private IActionResult BackToLogin(string message, string? returnUrl)
        {
            TempData["LoginError"] = message;
            return RedirectToPage("./Login", new { returnUrl });
        }

        private string SafeReturnUrl(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
    }
}
