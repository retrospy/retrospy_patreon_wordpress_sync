
using Patreon.Net;
using Patreon.Net.Models;

namespace retrospy_patreon_wordpress_sync
{


    class Program
    {

        private static PatreonClient? patreonClient;

        private static bool RefreshingRefreshToken = false;

        private static Task OnTokensRefreshedAsync(OAuthToken token)
        {
            RefreshingRefreshToken = true;
            Environment.SetEnvironmentVariable("PatreonRefreshToken", token.RefreshToken, EnvironmentVariableTarget.User);
            RefreshingRefreshToken = false;
            return Task.FromResult(token);
        }

        private static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();

            MappingAgent wp = new();
            if (patreonClient != null)
                wp.ValidateAndMoveSubscribers(patreonClient).GetAwaiter().GetResult();

            while (RefreshingRefreshToken) ;
            patreonClient?.Dispose();
        }

        private static async Task MainAsync(string[] args)
        {
            Config.PatreonClientAccessToken = Environment.GetEnvironmentVariable("PatreonClientAccessToken") ?? string.Empty;
            Config.PatreonClientId = Environment.GetEnvironmentVariable("PatreonClientId") ?? string.Empty;
            Config.PatreonRefreshToken = Environment.GetEnvironmentVariable("PatreonRefreshToken") ?? string.Empty;

            patreonClient = new PatreonClient(Config.PatreonClientAccessToken, Config.PatreonRefreshToken, Config.PatreonClientId);

            patreonClient.TokensRefreshedAsync += OnTokensRefreshedAsync;
        }
    }
}