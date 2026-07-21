using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
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

builder.Services.AddDbContext<HearditDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("HearditDbContextConnection") ?? throw new InvalidOperationException("Connection string 'HearditDbContextConnection' not found.")));

builder.Services.AddDefaultIdentity<HearditUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<HearditDbContext>();

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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
