using Heardit.Areas.Identity.Data;
using Heardit.Areas.Identity.Pages.Account;
using Heardit.Services;
using Heardit.Services.Email;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Heardit.Controllers
{
    /// <summary>Everything about your own account on one page: photo, email, password, Google, deletion.</summary>
    public class SettingsController : Controller
    {
        private readonly UserManager<HearditUser> _userManager;
        private readonly SignInManager<HearditUser> _signInManager;
        private readonly IAvatarService _avatars;
        private readonly IAccountEmails _emails;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(
            UserManager<HearditUser> userManager,
            SignInManager<HearditUser> signInManager,
            IAvatarService avatars,
            IAccountEmails emails,
            ILogger<SettingsController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _avatars = avatars;
            _emails = emails;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var logins = (await _userManager.GetLoginsAsync(user)).ToList();
            var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();

            return View(new SettingsViewModel
            {
                User = user,
                HasPassword = await _userManager.HasPasswordAsync(user),
                Logins = logins,
                AvailableProviders = schemes.Where(s => logins.All(l => l.LoginProvider != s.Name)).ToList()
            });
        }

        [HttpPost]
        [RequestSizeLimit(AvatarImage.MaxUploadBytes + 64 * 1024)]
        public async Task<IActionResult> UploadPhoto(IFormFile? photo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (photo == null)
            {
                return Back("Choose a photo to upload.", error: true);
            }

            await using var stream = photo.OpenReadStream();
            var result = await _avatars.SetAsync(user.Id, stream, photo.Length);
            switch (result.Status)
            {
                case AvatarUploadStatus.Saved:
                    await RefreshAsync(user.Id);
                    return Back("Your photo is updated.");
                case AvatarUploadStatus.TooLarge:
                    return Back("That photo is over 5 MB. Try a smaller one.", error: true);
                case AvatarUploadStatus.Empty:
                    return Back("Choose a photo to upload.", error: true);
                default:
                    return Back("That file isn't a photo we can read. Use a JPEG, PNG, WebP, GIF or HEIC.", error: true);
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemovePhoto()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return Challenge();
            }

            await _avatars.RemoveAsync(userId);
            await RefreshAsync(userId);
            return Back("Your photo is removed.");
        }

        [HttpPost]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> ResendVerification()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (user.EmailConfirmed)
            {
                return Back("Your email is already confirmed.");
            }

            var code = AccountTokens.Encode(await _userManager.GenerateEmailConfirmationTokenAsync(user));
            var link = Url.Page("/Account/ConfirmEmail", null, new { area = "Identity", userId = user.Id, code }, Request.Scheme)!;
            return await _emails.SendVerificationAsync(user.Email!, user.UserName!, link)
                ? Back($"Sent. Check {user.Email} for the link.")
                : Back("We couldn't send the email just now. Try again in a few minutes.", error: true);
        }

        [HttpPost]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> ChangeEmail(string? newEmail, string? currentPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            newEmail = newEmail?.Trim();
            if (string.IsNullOrEmpty(newEmail) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(newEmail))
            {
                return Back("Enter a valid email address.", error: true);
            }

            if (string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                return Back("That's already your email.", error: true);
            }

            // Moving the account to another inbox is as sensitive as a password change.
            if (await _userManager.HasPasswordAsync(user) && !await _userManager.CheckPasswordAsync(user, currentPassword ?? string.Empty))
            {
                return Back("Your current password wasn't right.", error: true);
            }

            if (await EmailTakenAsync(newEmail, user.Id))
            {
                return Back("Another account already uses that email.", error: true);
            }

            var code = AccountTokens.Encode(await _userManager.GenerateChangeEmailTokenAsync(user, newEmail));
            var link = Url.Page("/Account/ConfirmEmail", null, new { area = "Identity", userId = user.Id, email = newEmail, code }, Request.Scheme)!;
            return await _emails.SendEmailChangeAsync(newEmail, user.UserName!, link)
                ? Back($"Almost done: click the link we sent to {newEmail}. Your email stays {user.Email} until then.")
                : Back("We couldn't send the confirmation email just now. Try again in a few minutes.", error: true);
        }

        [HttpPost]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> ChangePassword(string? currentPassword, string? newPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var hasPassword = await _userManager.HasPasswordAsync(user);
            if (string.IsNullOrEmpty(newPassword))
            {
                return Back("Enter a new password.", error: true);
            }

            var result = hasPassword
                ? await _userManager.ChangePasswordAsync(user, currentPassword ?? string.Empty, newPassword)
                : await _userManager.AddPasswordAsync(user, newPassword);

            if (!result.Succeeded)
            {
                var message = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                    ? "Your current password wasn't right."
                    : string.Join(" ", result.Errors.Select(e => e.Description));
                return Back(message, error: true);
            }

            // The security stamp changed; re-issue this browser's cookie so it stays signed in.
            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("User changed their password.");
            return Back(hasPassword ? "Your password is changed." : "Password set. You can now log in with it too.");
        }

        [HttpPost]
        public async Task<IActionResult> RemoveLogin(string loginProvider, string providerKey)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var logins = await _userManager.GetLoginsAsync(user);
            if (!await _userManager.HasPasswordAsync(user) && logins.Count <= 1)
            {
                return Back("Set a password first, so you can still log in without it.", error: true);
            }

            var result = await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
            if (!result.Succeeded)
            {
                return Back("We couldn't disconnect that. Try again.", error: true);
            }

            await _signInManager.RefreshSignInAsync(user);
            return Back("Disconnected.");
        }

        [HttpPost]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> Delete(string? confirmUserName, string? currentPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!string.Equals(confirmUserName?.Trim(), user.UserName, StringComparison.Ordinal))
            {
                return Back($"Type your username, {user.UserName}, exactly to confirm.", error: true, anchor: "delete");
            }

            if (await _userManager.HasPasswordAsync(user) && !await _userManager.CheckPasswordAsync(user, currentPassword ?? string.Empty))
            {
                return Back("Your password wasn't right.", error: true, anchor: "delete");
            }

            // Reviews, likes, follows, the queue, favorites and the photo all cascade with the user row.
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return Back("We couldn't delete your account. Try again.", error: true, anchor: "delete");
            }

            await _signInManager.SignOutAsync();
            _logger.LogInformation("User deleted their account.");
            TempData["Flash"] = "Your account and everything in it is deleted. Thanks for listening.";
            return Redirect(Url.Content("~/Identity/Account/Login"));
        }

        private async Task<bool> EmailTakenAsync(string email, string exceptUserId)
        {
            try
            {
                var other = await _userManager.FindByEmailAsync(email);
                return other != null && other.Id != exceptUserId;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        /// <summary>Re-issue the cookie so the photo and verification claims match the database.</summary>
        private async Task RefreshAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                await _signInManager.RefreshSignInAsync(user);
            }
        }

        private IActionResult Back(string message, bool error = false, string? anchor = null)
        {
            TempData[error ? "FlashError" : "Flash"] = message;
            var url = Url.Action(nameof(Index))!;
            return Redirect(anchor == null ? url : $"{url}#{anchor}");
        }
    }
}
