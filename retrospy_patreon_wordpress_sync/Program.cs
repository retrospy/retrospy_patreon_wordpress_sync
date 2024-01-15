
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Patreon.Net;
using Patreon.Net.Models;
using System.Net.Http;
using System.Text;

namespace retrospy_patreon_wordpress_sync
{


    class Program
    {

        private static PatreonClient? patreonClient;

        private static void Main(string[] args)
        {
            Config.PatreonClientAccessToken = Environment.GetEnvironmentVariable("PatreonClientAccessToken") ?? string.Empty;
            Config.PatreonClientId = Environment.GetEnvironmentVariable("PatreonClientId") ?? string.Empty;
            Config.PatreonRefreshToken = Environment.GetEnvironmentVariable("PatreonRefreshToken") ?? string.Empty;

            RefreshToken(args);

            patreonClient = new PatreonClient(Config.PatreonClientAccessToken, Config.PatreonRefreshToken, Config.PatreonClientId);

            MappingAgent wp = new();
            if (patreonClient != null)
                wp.ValidateAndMoveSubscribers(patreonClient).GetAwaiter().GetResult();

            patreonClient?.Dispose();
        }

        private static void RefreshToken(string[] args)
        {
            string responseStr = string.Empty;
            var handler = new HttpClientHandler();
            var pHttpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://www.patreon.com")
            };
            pHttpClient.DefaultRequestHeaders.Add("ContentType", "application/x-www-form-urlencoded");
            pHttpClient.DefaultRequestHeaders.Add("User-Agent", "RetroSpy-Patreon-Sub-Mapping");
            var content = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = pHttpClient.PostAsync(string.Format("/api/oauth2/token?grant_type=refresh_token&refresh_token={0}&client_id={1}&client_secret={2}", Config.PatreonRefreshToken, Config.PatreonClientId, Config.PatreonClientAccessToken), content).Result;
            using (StreamReader stream = new(response.Content.ReadAsStreamAsync().Result))
            {
                responseStr = stream.ReadToEnd();
            }
            dynamic? tokenData = JsonConvert.DeserializeObject(responseStr);
            if (tokenData != null)
            {
                Config.PatreonClientAccessToken = tokenData?.access_token ?? Config.PatreonClientAccessToken;
                Config.PatreonRefreshToken = tokenData?.refresh_token ?? Config.PatreonRefreshToken;
                Environment.SetEnvironmentVariable("PatreonRefreshToken", Config.PatreonRefreshToken, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("PatreonClientAccessToken", Config.PatreonClientAccessToken, EnvironmentVariableTarget.User);
            }

        }
    }
}