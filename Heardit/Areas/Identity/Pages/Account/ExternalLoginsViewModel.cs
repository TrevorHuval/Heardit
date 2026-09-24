using Microsoft.AspNetCore.Authentication;

namespace Heardit.Areas.Identity.Pages.Account
{
    public record ExternalLoginsViewModel(IReadOnlyList<AuthenticationScheme> Schemes, string? ReturnUrl);
}
