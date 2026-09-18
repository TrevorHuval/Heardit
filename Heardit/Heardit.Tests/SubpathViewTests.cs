using System.Net;
using System.Security.Claims;
using Heardit.Areas.Identity.Data;
using Heardit.Controllers;
using Heardit.Models;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Heardit.Tests;

public class SubpathViewTests
{
    [Theory]
    [InlineData("")]
    [InlineData("/heardit")]
    public async Task Rendered_forms_and_pagination_preserve_the_application_path(string pathBase)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(SearchController).Assembly.GetName().Name
        });
        builder.Services.AddControllersWithViews();
        await using var app = builder.Build();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "View rendering"));
        context.Request.PathBase = pathBase;
        context.Request.Path = "/Search/_Search";
        context.Request.QueryString = new QueryString("?SearchString=ethereal+connection&page=2");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "reader")], "test"));
        var action = new ActionContext(context, new RouteData(), new ActionDescriptor());

        async Task<string> Render(string viewPath, object model)
        {
            var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
            var view = engine.GetView(null, viewPath, false);
            Assert.True(view.Success, string.Join(", ", view.SearchedLocations ?? []));
            using var writer = new StringWriter();
            var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
            var tempData = new TempDataDictionary(context, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
            await view.View.RenderAsync(new ViewContext(action, view.View, data, tempData, writer, new HtmlHelperOptions()));
            return WebUtility.HtmlDecode(writer.ToString());
        }

        var expected = $"{pathBase}/Search/_Search?SearchString=ethereal+connection&page=2";
        var feed = await Render("/Views/Shared/_FeedCard.cshtml", new FeedItemViewModel { Id = "track123", Name = "Test" });
        Assert.Contains($"name=\"returnUrl\" value=\"{expected}#track-track123\"", feed);

        var review = await Render("/Views/Shared/_ReviewPartial.cshtml", new ReviewViewModel
        {
            Review = new Review { ReviewId = "review123", UserId = "author", User = new HearditUser { UserName = "author" } }
        });
        Assert.Contains($"name=\"returnUrl\" value=\"{expected}#review-review123\"", review);

        var song = await Render("/Views/Songs/Song.cshtml", new SongViewModel
        {
            Song = new Song { Id = "track123", Title = "Test" },
            Reviews = new PagedList<Review> { Items = [], Page = 1, HasNext = false },
            HasFavoriteSlot = true
        });
        Assert.Equal(2, song.Split($"name=\"returnUrl\" value=\"{expected}\"").Length - 1);

        var pager = await Render("/Views/Shared/_Pager.cshtml", new PagerViewModel { Page = 2, HasNext = true });
        Assert.Contains($"href=\"{pathBase}/Search/_Search?SearchString=ethereal%20connection&page=1\"", pager);
        Assert.Contains($"href=\"{pathBase}/Search/_Search?SearchString=ethereal%20connection&page=3\"", pager);
        await app.StopAsync();
    }
}
