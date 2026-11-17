using System.Text.RegularExpressions;
using Heardit.Areas.Identity.Data;
using Heardit.Areas.Identity.Pages.Account;
using Heardit.Services;
using Heardit.Services.Email;
using Heardit.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Heardit.Tests;

public class SecurityHeadersTests
{
    private static string Directive(string name) =>
        SecurityHeaders.ContentSecurityPolicy.Split(';').Select(d => d.Trim()).Single(d => d.StartsWith(name + " "));

    [Fact]
    public void ScriptSrc_DoesNotAllowInlineOrEvalScript()
    {
        var scripts = Directive("script-src");

        Assert.DoesNotContain("'unsafe-inline'", scripts);
        Assert.DoesNotContain("'unsafe-eval'", scripts);
        Assert.DoesNotContain("*", scripts.Replace("https://*.spotifycdn.com", string.Empty));
    }

    [Fact]
    public void Apply_SetsEveryBaselineHeader()
    {
        var headers = new HeaderDictionary();

        SecurityHeaders.Apply(headers);

        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, headers["Content-Security-Policy"]);
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("SAMEORIGIN", headers["X-Frame-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);
        Assert.Contains("geolocation=()", headers["Permissions-Policy"].ToString());
    }

    /// <summary>The CSP only holds if no page relies on inline script, so the views are checked too.</summary>
    [Fact]
    public void NoView_UsesAnInlineScriptOrEventHandler()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Heardit.csproj")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var views = new[] { "Views", "Areas" }
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root!.FullName, dir), "*.cshtml", SearchOption.AllDirectories))
            .ToList();
        Assert.NotEmpty(views);

        var inlineScript = new Regex(@"<script(?![^>]*\bsrc=)[^>]*>", RegexOptions.IgnoreCase);
        var handler = new Regex(@"<[^>@]*\son[a-z]+\s*=", RegexOptions.IgnoreCase);
        var scriptUrl = new Regex(@"(href|src|action)\s*=\s*[""']\s*javascript:", RegexOptions.IgnoreCase);

        foreach (var view in views)
        {
            var markup = File.ReadAllText(view);
            Assert.False(inlineScript.IsMatch(markup), $"{view} has an inline <script>; the CSP would block it.");
            Assert.False(handler.IsMatch(markup), $"{view} has an inline event handler; the CSP would block it.");
            Assert.False(scriptUrl.IsMatch(markup), $"{view} has a javascript: URL; the CSP would block it.");
        }
    }
}

public class DecodeGateTests
{
    [Fact]
    public async Task RunAsync_NeverRunsMoreJobsAtOnceThanItsLimit()
    {
        var gate = new DecodeGate(1);
        var running = 0;
        var peak = 0;

        var jobs = Enumerable.Range(0, 12).Select(i => gate.RunAsync(() =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref peak, now);
            Thread.Sleep(15);
            Interlocked.Decrement(ref running);
            return i;
        }));

        var results = await Task.WhenAll(jobs);

        Assert.Equal(1, peak);
        Assert.Equal(Enumerable.Range(0, 12), results.OrderBy(r => r));
    }

    [Fact]
    public async Task RunAsync_FreesTheSlotWhenAJobThrows()
    {
        var gate = new DecodeGate(1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync<int>(() => throw new InvalidOperationException()));

        Assert.Equal(7, await gate.RunAsync(() => 7).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task AvatarDecoding_IsGatedToOneAtATime()
    {
        // The limit that keeps a burst of 40-megapixel uploads from exhausting the host.
        var gate = AvatarImage.Gate;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var first = gate.RunAsync(() => { entered.Set(); release.Wait(); return 1; });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var second = gate.RunAsync(() => 2);

        Assert.NotSame(second, await Task.WhenAny(second, Task.Delay(150)));
        release.Set();
        Assert.Equal(2, await second);
        Assert.Equal(1, await first);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while (value > (seen = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }
}

public class PlainEmailAddressTests
{
    [Theory]
    [InlineData("name@example.com")]
    [InlineData("first.last+tag@mail.example.co.uk")]
    [InlineData("o'brien_99@example.io")]
    public void AcceptsABareAddress(string value)
    {
        Assert.True(PlainEmailAddressAttribute.IsPlainAddress(value));
        Assert.True(new PlainEmailAddressAttribute().IsValid(value));
    }

    [Theory]
    [InlineData("Someone <victim@example.com>")]       // display-name form: a second spelling of one inbox
    [InlineData("victim@example.com,attacker")]        // read as a recipient list
    [InlineData("victim@example.com, x@evil.example")]
    [InlineData("\"a b\"@example.com")]
    [InlineData("victim@example.com\r\nBcc: x@evil.example")]
    [InlineData("victim@example.com (comment)")]
    [InlineData("name@localhost")]
    [InlineData("no-at-sign")]
    [InlineData("@example.com")]
    public void RejectsAnythingThatIsNotOneBareAddress(string value)
    {
        Assert.False(PlainEmailAddressAttribute.IsPlainAddress(value));
        Assert.False(new PlainEmailAddressAttribute().IsValid(value));
    }

    [Fact]
    public void RejectsAnAddressLongerThanTheLimit()
    {
        Assert.False(PlainEmailAddressAttribute.IsPlainAddress(new string('a', 250) + "@example.com"));
    }

    [Fact]
    public void LeavesEmptyToRequired()
    {
        Assert.True(new PlainEmailAddressAttribute().IsValid(null));
        Assert.True(new PlainEmailAddressAttribute().IsValid(""));
    }

    [Theory]
    [InlineData("victim@example.com,attacker@evil.example")]
    [InlineData("not an address")]
    public async Task SmtpSender_RefusesARecipientListInsteadOfSendingOrThrowing(string to)
    {
        // Port 1 on loopback: if the sender got as far as connecting, this would fail differently (or hang).
        var sender = new SmtpEmailSender(
            Microsoft.Extensions.Options.Options.Create(new EmailOptions { Host = "127.0.0.1", Port = 1, From = "noreply@example.com" }),
            NullLogger<SmtpEmailSender>.Instance);

        Assert.False(await sender.SendAsync(new EmailMessage(to, "Subject", "<p>Hi</p>", "Hi")));
    }
}

public class ConfirmEmailTests
{
    private static (ConfirmEmailModel Page, UserManager<HearditUser> Users) Build(HearditUser user)
    {
        var users = TestData.SubstituteUserManager();
        users.FindByIdAsync(user.Id).Returns(user);
        users.ConfirmEmailAsync(user, Arg.Any<string>()).Returns(IdentityResult.Success);
        users.ChangeEmailAsync(user, Arg.Any<string>(), Arg.Any<string>()).Returns(IdentityResult.Success);

        var signIn = Substitute.For<SignInManager<HearditUser>>(
            users,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<HearditUser>>(),
            null!, null!, null!, null!);

        var page = new ConfirmEmailModel(users, signIn)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };
        return (page, users);
    }

    [Fact]
    public async Task OpeningTheLink_ConfirmsNothing_AndOffersTheButton()
    {
        var user = TestData.MakeUser("newbie");
        var (page, users) = Build(user);
        var code = AccountTokens.Encode("token");

        page.OnGet(user.Id, code);

        Assert.True(page.Pending);
        Assert.False(page.Succeeded);
        Assert.Equal(user.Id, page.UserId);
        Assert.Equal(code, page.Code);
        await users.DidNotReceiveWithAnyArgs().ConfirmEmailAsync(default!, default!);
        await users.DidNotReceiveWithAnyArgs().ChangeEmailAsync(default!, default!, default!);
        await users.DidNotReceiveWithAnyArgs().FindByIdAsync(default!);
    }

    [Fact]
    public async Task OpeningAnEmailChangeLink_ChangesNothing()
    {
        var user = TestData.MakeUser("mover");
        var (page, users) = Build(user);

        page.OnGet(user.Id, AccountTokens.Encode("token"), "new@example.com");

        Assert.True(page.Pending);
        Assert.True(page.WasChange);
        Assert.Equal("new@example.com", page.Email);
        await users.DidNotReceiveWithAnyArgs().ChangeEmailAsync(default!, default!, default!);
    }

    [Fact]
    public void AMangledLink_ShowsTheDeadLinkPage()
    {
        var user = TestData.MakeUser("newbie");
        var (page, _) = Build(user);

        page.OnGet(user.Id, "%%%not-base64%%%");

        Assert.False(page.Pending);
        Assert.False(page.Succeeded);
    }

    [Fact]
    public async Task PressingTheButton_ConfirmsTheAddress()
    {
        var user = TestData.MakeUser("newbie");
        var (page, users) = Build(user);

        await page.OnPostAsync(user.Id, AccountTokens.Encode("token"));

        Assert.True(page.Succeeded);
        Assert.False(page.Pending);
        await users.Received(1).ConfirmEmailAsync(user, "token");
    }

    [Fact]
    public async Task PressingTheButton_OnAChangeLink_MovesTheAccountToTheNewAddress()
    {
        var user = TestData.MakeUser("mover");
        var (page, users) = Build(user);

        await page.OnPostAsync(user.Id, AccountTokens.Encode("token"), "new@example.com");

        Assert.True(page.Succeeded);
        await users.Received(1).ChangeEmailAsync(user, "new@example.com", "token");
    }
}
