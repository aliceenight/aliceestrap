using Aliceestrap.RobloxInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Aliceestrap
{
    public class CookiesManager
    {
        private CookieState _state = CookieState.Unknown;

        public EventHandler<CookieState>? StateChanged;
        public CookieState State {
            get => _state;
            set {
                _state = value;
                StateChanged?.Invoke(this, value);
            }
        }
        public bool Loaded => Enabled && State == CookieState.Success;
        private bool Enabled => App.Settings.Prop.AllowCookieAccess;

        private string AuthCookie = string.Empty;
        private const string AuthCookieName = ".ROBLOSECURITY";
        private const string SupportedVersion = "1";
        private const string AuthPattern = $@"\t{AuthCookieName}\t(.+?)(;|$)";
        private string CookiesPath => Path.Combine(Paths.Roblox, "LocalStorage", Deployment.IsDefaultRobloxDomain ? "RobloxCookies.dat" : $"{Deployment.RobloxDomain}_RobloxCookies.dat");

        public async Task<HttpResponseMessage> AuthRequest(HttpRequestMessage request)
        {
            string? host = request.RequestUri?.Host;

            if (host is null)
                throw new ArgumentNullException("Host cannot be null");

            if (
                !host.Equals(Deployment.RobloxDomain, StringComparison.OrdinalIgnoreCase) &&
                !host.EndsWith("." + Deployment.RobloxDomain, StringComparison.OrdinalIgnoreCase)
                )
                throw new HttpRequestException($"Host must end with Roblox domain ({Deployment.RobloxDomain})");

            if (!Enabled)
                throw new NullReferenceException("Cookie access is not enabled");

            request.Headers.Add("Cookie", $".ROBLOSECURITY={AuthCookie}");
            var response = await App.HttpClient.SendAsync(request);

            return response;
        }

        public async Task<HttpResponseMessage> AuthGet(Uri? uri) => await AuthRequest(new HttpRequestMessage { RequestUri = uri, Method = HttpMethod.Get });
        public async Task<HttpResponseMessage> AuthPost(Uri? uri, HttpContent? content) => await AuthRequest(new HttpRequestMessage { RequestUri = uri, Content = content, Method = HttpMethod.Post });

        public async Task<AuthenticatedUser?> GetAuthenticated()
        {
            const string LOG_IDENT = "CookiesManager::GetAuthenticated";
            
            try
            {
                Uri apiUrl = UrlBuilder.BuildApiUrl("users", "v1/users/authenticated");
                HttpResponseMessage response = await AuthGet(apiUrl);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                AuthenticatedUser user = JsonSerializer.Deserialize<AuthenticatedUser>(content)!;

                return user;
            }
            catch (HttpRequestException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to get authenticated user");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return null;
        }

        public async Task LoadCookies()
        {
            const string LOG_IDENT = "CookiesManager::LoadCookies";

            if (!Enabled)
            {
                State = CookieState.NotAllowed;
                App.Logger.WriteLine(LOG_IDENT, "Cookie access not allowed");
                return;
            }

            if (!string.IsNullOrEmpty(AuthCookie))
            {
                App.Logger.WriteLine(LOG_IDENT, "Cookie was already loaded!");
                return;
            }

            if (!File.Exists(CookiesPath))
            {
                State = CookieState.NotFound;
                App.Logger.WriteLine(LOG_IDENT, "Cookie file not found");
                return;
            }

            try
            {
                string content = File.ReadAllText(CookiesPath);
                var cookies = JsonSerializer.Deserialize<RobloxCookies>(content)!;

                if (cookies.Version != SupportedVersion)
                    App.Logger.WriteLine(LOG_IDENT, $"Unknown cookie version: {cookies.Version}");

                byte[] encryptedData = Convert.FromBase64String(cookies.Cookies);
                byte[] unencryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);

                string rawCookies = Encoding.UTF8.GetString(unencryptedData);
                Match authCookieMatch = Regex.Match(rawCookies, AuthPattern);

                if (!authCookieMatch.Success)
                {
                    State = CookieState.Invalid;
                    App.Logger.WriteLine(LOG_IDENT, "Regex failed for cookies");
                    return;
                }

                string authCookie = authCookieMatch.Groups[1].Value;
                AuthCookie = authCookie;

                AuthenticatedUser? user = await GetAuthenticated();
                if (user is null || user?.Id == 0)
                {
                    State = CookieState.Invalid;
                    App.Logger.WriteLine(LOG_IDENT, "Cookie is invalid");
                    return;
                }

                State = CookieState.Success;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load cookie!");
                App.Logger.WriteException(LOG_IDENT, ex); 

                State = CookieState.Failed;
            }

            return;
        }
    }
}
