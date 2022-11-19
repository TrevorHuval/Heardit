using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Heardit.Data;
using Heardit.Models;
using Heardit;
using SpotifyAPI.Web;
using Heardit.Controllers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<HearditContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HearditContext") ?? throw new InvalidOperationException("Connection string 'HearditContext' not found.")));

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

//var spotify = new SpotifyClient(await SpotifyController.GetAccessToken(), tokenType: "Bearer");

app.Run();
