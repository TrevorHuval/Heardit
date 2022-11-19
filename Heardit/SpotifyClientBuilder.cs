using Heardit.Controllers;
using Microsoft.AspNetCore.Authentication;
using SpotifyAPI.Web;

namespace Heardit
{
    public class SpotifyClientBuilder
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly SpotifyClientConfig _spotifyClientConfig;

        public SpotifyClientBuilder(IHttpContextAccessor httpContextAccessor, SpotifyClientConfig spotifyClientConfig)
        {
            _httpContextAccessor = httpContextAccessor;
            _spotifyClientConfig = spotifyClientConfig;
        }

        //public SpotifyClient BuildClient()
        //{
        //    var token = SpotifyController.GetAccessToken();

        //    return new SpotifyClient(_spotifyClientConfig.WithToken(token));
        //}
    }
}
