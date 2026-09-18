using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Heardit.Models;
using Heardit.Options;
using Heardit.Services;
using Heardit.Areas.Identity.Data;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

var builder = WebApplication.CreateBuilder(args);

// Optional sub-path when hosted behind a reverse proxy next to other apps on one domain
// (PathBase=/heardit for https://example.com/heardit). Normalised once; used for the middleware
// and to scope the cookies so they never collide with another app on the same host.
var pathBase = builder.Configuration["PathBase"];
pathBase = string.IsNullOrWhiteSpace(pathBase) ? null : "/" + pathBase.Trim().Trim('/');

builder.Services.AddDbContext<HearditDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("HearditDbContextConnection") ?? throw new InvalidOperationException("Connection string 'HearditDbContextConnection' not found.")));

builder.Services.AddDefaultIdentity<HearditUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<HearditDbContext>();

// Identity's default cookie name is the same in every ASP.NET app. Two of them on one domain
// (this app and Plannit, say) would overwrite each other's login, so name and scope ours.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Heardit.Auth";
    options.Cookie.Path = pathBase ?? "/";
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "Heardit.Antiforgery";
    options.Cookie.Path = pathBase ?? "/";
});

// Persist the key ring outside the container so a redeploy doesn't log everyone out and
// invalidate every antiforgery token in flight. Unset means the default in-container location.
var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath))
        .SetApplicationName("Heardit");
}

builder.Services.AddHealthChecks();

// Spotify configuration bound from user-secrets / environment (never committed).
builder.Services.AddOptions<SpotifyOptions>()
    .BindConfiguration(SpotifyOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// A single long-lived Spotify client; ClientCredentialsAuthenticator handles token acquisition/refresh internally.
builder.Services.AddSingleton<ISpotifyClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<SpotifyOptions>>().Value;
    var config = SpotifyClientConfig
        .CreateDefault()
        .WithAuthenticator(new ClientCredentialsAuthenticator(options.ClientId, options.ClientSecret));
    return new SpotifyClient(config);
});

// Application services.
// The Spotify cache is bounded so search traffic can't grow it without limit; entries declare a size
// in SpotifyService (roughly their row count), and the cache evicts once the budget is spent.
builder.Services.AddMemoryCache(options => options.SizeLimit = 1024);
builder.Services.AddScoped<ISpotifyService, SpotifyService>();
builder.Services.AddScoped<ISongService, SongService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IListenLaterService, ListenLaterService>();

// Require an authenticated user by default; opt out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Throttle the endpoints that call the Spotify API, partitioned per user (or client IP).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("spotify", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(10),
                PermitLimit = 10,
                QueueLimit = 0
            }));
});

// Add services to the container. Every unsafe (POST/PUT/DELETE) request is antiforgery-validated.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Behind a TLS-terminating reverse proxy (Caddy/nginx), trust X-Forwarded-For/Proto so that
// HTTPS redirection, HSTS, and secure-cookie logic see the original scheme and client IP.
// KnownNetworks/KnownProxies are cleared because the app is only reachable via the trusted proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.

// First, so routing, generated URLs, static files and login redirects all carry the prefix.
if (pathBase != null)
{
    app.UsePathBase(pathBase);
}

// Must run before any middleware that inspects the scheme or client IP (HTTPS redirect, HSTS, cookies).
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Render the friendly error view for unhandled error status codes (e.g. 404/403 from controllers).
app.UseStatusCodePagesWithReExecute("/Home/Error");

// Baseline security headers. CSP permits only the Spotify embeds the views use; the icon that
// once required the remote Font Awesome kit is now an inline SVG, so no external font/script hosts.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://open.spotify.com https://*.spotifycdn.com; " +
        "style-src 'self' 'unsafe-inline'; " +
        "font-src 'self' data:; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' https://*.spotify.com https://*.spotifycdn.com; " +
        "frame-src https://open.spotify.com https://*.spotify.com https://*.spotifycdn.com; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";
    await next();
});

// The health probe is hit over plain HTTP from inside the host network, with no forwarded
// scheme to satisfy the redirect, so it is the one path that skips it.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/healthz"),
    branch => branch.UseHttpsRedirection());

// Everything under wwwroot is either fingerprinted (asp-append-version adds ?v=) or a vendored
// library that only changes with a deploy, so let browsers keep it.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var versioned = ctx.Context.Request.Query.ContainsKey("v");
        ctx.Context.Response.Headers.CacheControl = versioned
            ? "public,max-age=31536000,immutable"
            : "public,max-age=86400";
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Liveness for the reverse proxy / compose. Anonymous, and outside the auth fallback policy.
app.MapHealthChecks("/healthz").AllowAnonymous();

app.Run();
