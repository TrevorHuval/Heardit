using System.Security.Claims;
using Heardit.Areas.Identity.Data;
using Heardit.Controllers;
using Heardit.Services;
using Heardit.Services.Email;
using Heardit.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SkiaSharp;

namespace Heardit.Tests;

public class AvatarImageTests
{
    private static byte[] Png(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKCodec Decode(byte[] bytes) => SKCodec.Create(SKData.CreateCopy(bytes));

    [Fact]
    public void Process_WidePhoto_BecomesA256SquareWebP()
    {
        var result = AvatarImage.Process(Png(800, 400, SKColors.OrangeRed));

        Assert.NotNull(result);
        using var codec = Decode(result!);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.Equal(256, codec.Info.Width);
        Assert.Equal(256, codec.Info.Height);
    }

    [Fact]
    public void Process_SmallPhoto_IsCroppedButNeverUpscaled()
    {
        var result = AvatarImage.Process(Png(120, 90, SKColors.Teal));

        using var codec = Decode(result!);
        Assert.Equal(90, codec.Info.Width);
        Assert.Equal(90, codec.Info.Height);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 })]   // "%PDF-1"
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x3E })]          // "<svg>" is not accepted
    public void Process_NotAnImageWeAccept_ReturnsNull(byte[] bytes)
    {
        Assert.Null(AvatarImage.Process(bytes));
    }
}

public class AvatarServiceTests
{
    private static byte[] Png()
    {
        using var bitmap = new SKBitmap(300, 300);
        bitmap.Erase(SKColors.MediumPurple);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    [Fact]
    public async Task SetGetRemove_RoundTrip_AndTheVersionTracksThePhoto()
    {
        using var db = new SqliteInMemoryDb();
        var user = TestData.MakeUser("pic");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
        }

        var bytes = Png();
        using (var ctx = db.CreateContext())
        {
            var saved = await new AvatarService(ctx).SetAsync(user.Id, new MemoryStream(bytes), bytes.Length);
            Assert.Equal(AvatarUploadStatus.Saved, saved.Status);
            Assert.Equal(16, saved.Version!.Length);
        }

        using (var ctx = db.CreateContext())
        {
            var stored = await new AvatarService(ctx).GetAsync(user.Id);
            Assert.NotNull(stored);
            Assert.Equal("image/webp", stored!.ContentType);
            Assert.Equal((await ctx.Users.SingleAsync(u => u.Id == user.Id)).AvatarVersion, stored.Version);

            await new AvatarService(ctx).RemoveAsync(user.Id);
        }

        using (var ctx = db.CreateContext())
        {
            Assert.Null(await new AvatarService(ctx).GetAsync(user.Id));
            Assert.Null((await ctx.Users.SingleAsync(u => u.Id == user.Id)).AvatarVersion);
        }
    }

    [Fact]
    public async Task SetAsync_RejectsOversizedAndUnreadableUploads_WithoutTouchingTheUser()
    {
        using var db = new SqliteInMemoryDb();
        var user = TestData.MakeUser("pic");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var service = new AvatarService(ctx);

        Assert.Equal(AvatarUploadStatus.TooLarge,
            (await service.SetAsync(user.Id, Stream.Null, AvatarImage.MaxUploadBytes + 1)).Status);
        Assert.Equal(AvatarUploadStatus.Empty, (await service.SetAsync(user.Id, Stream.Null, 0)).Status);
        var junk = new byte[] { 1, 2, 3, 4 };
        Assert.Equal(AvatarUploadStatus.Unsupported,
            (await service.SetAsync(user.Id, new MemoryStream(junk), junk.Length)).Status);

        Assert.Null((await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).AvatarVersion);
    }

    [Fact]
    public async Task DeletingTheUser_DeletesTheirPhoto()
    {
        using var db = new SqliteInMemoryDb();
        var user = TestData.MakeUser("gone");
        var bytes = Png();
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
            await new AvatarService(seed).SetAsync(user.Id, new MemoryStream(bytes), bytes.Length);
        }

        using (var ctx = db.CreateContext())
        {
            ctx.Users.Remove(await ctx.Users.SingleAsync(u => u.Id == user.Id));
            await ctx.SaveChangesAsync();
        }

        using var check = db.CreateContext();
        Assert.Equal(0, await check.UserAvatars.CountAsync());
    }
}

public class ExternalAccountsTests
{
    [Theory]
    [InlineData(false, false, false, ExternalSignUpOutcome.CreateNew)]
    [InlineData(true, true, true, ExternalSignUpOutcome.LinkToExisting)]
    [InlineData(true, false, true, ExternalSignUpOutcome.SignInFirst)]   // our side never proved the address
    [InlineData(true, true, false, ExternalSignUpOutcome.SignInFirst)]   // Google's side didn't
    [InlineData(true, false, false, ExternalSignUpOutcome.SignInFirst)]
    public void Decide_OnlyLinksWhenBothSidesVerifiedTheSameAddress(
        bool accountExists, bool accountConfirmed, bool providerVerified, ExternalSignUpOutcome expected)
    {
        var existing = accountExists ? new HearditUser { EmailConfirmed = accountConfirmed } : null;

        Assert.Equal(expected, ExternalAccounts.Decide(existing, providerVerified));
    }

    [Fact]
    public void ProviderVerifiedEmail_ReadsTheMappedGoogleClaim()
    {
        var verified = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ExternalAccounts.EmailVerifiedClaim, "True") }));
        var unverified = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ExternalAccounts.EmailVerifiedClaim, "false") }));

        Assert.True(ExternalAccounts.ProviderVerifiedEmail(verified));
        Assert.False(ExternalAccounts.ProviderVerifiedEmail(unverified));
        Assert.False(ExternalAccounts.ProviderVerifiedEmail(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Theory]
    [InlineData("José Núñez", null, "Jose.Nunez")]
    [InlineData(null, "night.owl+music@gmail.com", "night.owl")]
    [InlineData("  Trevor  ", null, "Trevor")]
    [InlineData("Ab", null, "Ab11")]                                   // padded up to the minimum
    [InlineData("🎧🎶", "x@y.com", "1111")]                             // nothing usable survives
    [InlineData("An extremely long display name that goes on and on", null, "An.extremely.long.display.name")]
    public void SuggestUserName_FitsTheUsernameRules(string? name, string? email, string expected)
    {
        var suggestion = ExternalAccounts.SuggestUserName(name, email);

        Assert.Equal(expected, suggestion);
        Assert.Matches(UserNameRules.Pattern, suggestion);
        Assert.InRange(suggestion.Length, UserNameRules.MinLength, UserNameRules.MaxLength);
    }
}

public class VerificationGateTests
{
    [Theory]
    [InlineData(false, false, true)]   // grandfathered, never confirmed: still posts
    [InlineData(true, false, false)]   // new and unconfirmed: can't yet
    [InlineData(true, true, true)]
    public void CanPost_FollowsConfirmationUnlessGrandfathered(bool mustVerify, bool confirmed, bool expected)
    {
        Assert.Equal(expected, new HearditUser { MustVerifyEmail = mustVerify, EmailConfirmed = confirmed }.CanPost);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, true)]            // a cookie from before the claim existed
    public void CanPostReviews_ReadsTheCookieClaim(string? value, bool expected)
    {
        var claims = value == null ? Array.Empty<Claim>() : new[] { new Claim(HearditClaims.CanPost, value) };

        Assert.Equal(expected, new ClaimsPrincipal(new ClaimsIdentity(claims, "test")).CanPostReviews());
    }

    private static (ActionExecutingContext Context, Controller Controller) Executing(bool canPost, string? returnUrl)
    {
        var services = new ServiceCollection()
            .AddSingleton<IUrlHelperFactory, UrlHelperFactory>()
            .AddSingleton(Substitute.For<ITempDataDictionaryFactory>())
            .BuildServiceProvider();

        var http = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(HearditClaims.CanPost, canPost ? "true" : "false") }, "test"))
        };
        if (returnUrl != null)
        {
            http.Request.ContentType = "application/x-www-form-urlencoded";
            http.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["returnUrl"] = returnUrl });
        }

        var controller = Substitute.For<Controller>();
        controller.TempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>());
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return (new ActionExecutingContext(action, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller), controller);
    }

    [Fact]
    public void Gate_LetsVerifiedAccountsThrough()
    {
        var (context, _) = Executing(canPost: true, returnUrl: "/Songs?songId=x");

        new RequireVerifiedEmailAttribute().OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void Gate_SendsUnverifiedAccountsBackWithANote()
    {
        var (context, controller) = Executing(canPost: false, returnUrl: "/heardit/Songs?songId=x#composer");

        new RequireVerifiedEmailAttribute().OnActionExecuting(context);

        var redirect = Assert.IsType<LocalRedirectResult>(context.Result);
        Assert.Equal("/heardit/Songs?songId=x#composer", redirect.Url);
        Assert.Equal(RequireVerifiedEmailAttribute.Message, controller.TempData["FlashError"]);
    }

    [Fact]
    public void Gate_IgnoresAnOffSiteReturnUrl()
    {
        var (context, _) = Executing(canPost: false, returnUrl: "https://evil.example/phish");

        new RequireVerifiedEmailAttribute().OnActionExecuting(context);

        Assert.Equal("~/", Assert.IsType<LocalRedirectResult>(context.Result).Url);
    }
}

public class AccountEmailsTests
{
    [Fact]
    public void Compose_EscapesTheUsernameAndLinkInHtml_AndKeepsATextVersion()
    {
        var message = AccountEmails.Compose(
            "to@example.com", "Subject", "<b>eve</b>", "Lead", "Go", "https://x.example/c?a=1&b=2", "Footer");

        Assert.Contains("&lt;b&gt;eve&lt;/b&gt;", message.Html);
        Assert.DoesNotContain("<b>eve</b>", message.Html);
        Assert.Contains("a=1&amp;b=2", message.Html);
        Assert.Contains("Go: https://x.example/c?a=1&b=2", message.Text);
    }

    [Fact]
    public async Task SendVerification_HandsTheLinkToTheSender()
    {
        var sender = Substitute.For<IEmailSender>();
        sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(true);

        var sent = await new AccountEmails(sender).SendVerificationAsync("to@example.com", "maya", "https://h/confirm?code=1");

        Assert.True(sent);
        await sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m != null && m.To == "to@example.com" && m.Text.Contains("https://h/confirm?code=1")),
            Arg.Any<CancellationToken>());
    }
}

public class AntiforgeryFailureFilterTests
{
    private static (ResultExecutingContext Context, ITempDataDictionary TempData) Failed(string path, bool signedIn, string? referer = null, string query = "")
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Request.QueryString = new QueryString(query);
        http.Request.Host = new HostString("trevorhuval.com");
        if (referer != null)
        {
            http.Request.Headers.Referer = referer;
        }
        http.User = signedIn
            ? new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "u1") }, "test"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        var tempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>());
        var factory = Substitute.For<ITempDataDictionaryFactory>();
        factory.GetTempData(http).Returns(tempData);

        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var context = new ResultExecutingContext(action, new List<IFilterMetadata>(),
            new Microsoft.AspNetCore.Mvc.AntiforgeryValidationFailedResult(), controller: new object());

        new AntiforgeryFailureFilter(factory, Microsoft.Extensions.Logging.Abstractions.NullLogger<AntiforgeryFailureFilter>.Instance)
            .OnResultExecuting(context);
        return (context, tempData);
    }

    [Fact]
    public void LoggingInWhenAlreadySignedIn_GoesStraightToTheDestination()
    {
        var (context, tempData) = Failed("/Identity/Account/Login", signedIn: true, query: "?returnUrl=%2Fheardit%2FSettings");

        Assert.Equal("/heardit/Settings", Assert.IsType<LocalRedirectResult>(context.Result).Url);
        Assert.False(tempData.ContainsKey("FlashError"));
    }

    [Fact]
    public void AStaleFormElsewhere_ReturnsToThePageItWasOn_WithANote()
    {
        var (context, tempData) = Failed("/Review/ToggleLike", signedIn: true, referer: "https://trevorhuval.com/heardit/Songs?songId=abc");

        Assert.Equal("/heardit/Songs?songId=abc", Assert.IsType<LocalRedirectResult>(context.Result).Url);
        Assert.Contains("out of date", (string)tempData["FlashError"]!);
    }

    [Fact]
    public void AnOffSiteReferrer_IsIgnored()
    {
        var (context, _) = Failed("/Review/ToggleLike", signedIn: true, referer: "https://evil.example/heardit/x");

        Assert.Equal("~/", Assert.IsType<LocalRedirectResult>(context.Result).Url);
    }

    [Fact]
    public void OtherResults_AreLeftAlone()
    {
        var http = new DefaultHttpContext();
        var context = new ResultExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(), new OkResult(), controller: new object());

        new AntiforgeryFailureFilter(Substitute.For<ITempDataDictionaryFactory>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AntiforgeryFailureFilter>.Instance).OnResultExecuting(context);

        Assert.IsType<OkResult>(context.Result);
    }
}
