using imdbLite.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SpotifyAPI.Web;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using SpotifyAPI.Web.Http;

namespace imdbLite.Controllers
{
    public class SpotifyController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public static SpotifyClient GetSpotifyClient()
        {
            var config = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new ClientCredentialsAuthenticator("a0985cbe9a2c49409b4938fbc16de10b", "b32e00279cc64d2dad1990e5860ce35d")); // takes care of access tokens
            var spotify = new SpotifyClient(config);
            return spotify;
        }

        public static async Task<string> GetAccessToken()
        {

            SpotifyToken token = new SpotifyToken();
            string postString = string.Format("grant_type=client_credentials");

            byte[] byteArray = Encoding.UTF8.GetBytes(postString);
            string url = "https://accounts.spotify.com/api/token";

            WebRequest request = WebRequest.Create(url);
            request.Method = "POST";
            request.Headers.Add("Authorization", "Basic YTA5ODVjYmU5YTJjNDk0MDliNDkzOGZiYzE2ZGUxMGI6YjMyZTAwMjc5Y2M2NGQyZGFkMTk5MGU1ODYwY2UzNWQ="); request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteArray.Length;

            using (Stream dataStream = request.GetRequestStream())
            {
                dataStream.Write(byteArray, 0, byteArray.Length);
                using (WebResponse response = await request.GetResponseAsync())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            string responseFromServer = reader.ReadToEnd();
                            token = JsonConvert.DeserializeObject<SpotifyToken>(responseFromServer)!;
                        }
                    }
                }
            }

            return token.access_token;
        }

        
    }
}
