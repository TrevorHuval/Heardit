using Microsoft.EntityFrameworkCore;
using Heardit.Models;
using Heardit.Options;
using Heardit.Areas.Identity.Data;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<HearditDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HearditDbContextConnection") ?? throw new InvalidOperationException("Connection string 'HearditDbContextConnection' not found.")));

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

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    SeedData.Initialize(services);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
