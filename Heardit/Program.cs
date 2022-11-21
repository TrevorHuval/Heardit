using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Heardit.Models;
using Heardit;
using SpotifyAPI.Web;
using Heardit.Controllers;
using Microsoft.AspNetCore.Identity;
using Heardit.Areas.Identity.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<HearditDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HearditDbContextConnection") ?? throw new InvalidOperationException("Connection string 'HearditDbContextConnection' not found.")));

builder.Services.AddDefaultIdentity<HearditUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<HearditDbContext>();

// Add services to the container.
builder.Services.AddControllersWithViews();
//builder.Services.AddSingleton(SpotifyClientConfig.CreateDefault());
//builder.Services.AddScoped<SpotifyClientBuilder>();

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
app.UseAuthentication();;

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

//var spotify = new SpotifyClient(await SpotifyController.GetAccessToken(), tokenType: "Bearer");

app.Run();
