#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VRC.Core;
using VRC.SDKBase.Editor.Api;

namespace NekoSune.Tools
{
    /// <summary>
    /// Switches the VRChat SDK between saved accounts using only the SDK's own login/credential calls,
    /// the same ones the SDK control panel uses. Never calls APIUser.Logout on a saved account, because
    /// that ends the session on VRChat's side and the saved login would stop working.
    /// </summary>
    internal static class VrcSession
    {
        /// <summary>A copy of what the SDK keeps for a logged-in user.</summary>
        internal sealed class Snapshot
        {
            public string humanName = "", provider = "vrchat", providerUserId = "", auth = "", twoFactor = "";
            public bool HasToken => !string.IsNullOrEmpty(auth);
        }

        /// <summary>The account a session token really belongs to, according to VRChat.</summary>
        internal sealed class Identity
        {
            public string id;
            public string displayName;
            public string username;
        }

        internal enum LoginState { Success, NeedsCode, Failed }

        internal sealed class LoginResult
        {
            public LoginState state;
            public string error;
            public string codeType;   // API2FA.* constant when state == NeedsCode
            public Snapshot session;
            public Identity identity;
        }

        private const int FetchTimeoutMs = 20000;

        /// <summary>The session the SDK is using right now (may be empty when logged out).</summary>
        public static Snapshot Capture()
        {
            if (!ApiCredentials.IsLoaded()) ApiCredentials.Load();
            return new Snapshot
            {
                humanName = ApiCredentials.GetHumanName() ?? "",
                provider = ApiCredentials.GetAuthTokenProvider() ?? "vrchat",
                providerUserId = ApiCredentials.GetAuthTokenProviderUserId() ?? "",
                auth = ApiCredentials.GetAuthToken() ?? "",
                twoFactor = ApiCredentials.GetTwoFactorAuthToken() ?? ""
            };
        }

        /// <summary>
        /// Puts <paramref name="s"/> into the SDK and asks VRChat who it belongs to.
        /// Returns null when the session is expired or invalid. Also refreshes APIUser.CurrentUser,
        /// which the SDK's builder uses for its permission and ownership checks.
        /// </summary>
        public static async Task<Identity> Apply(Snapshot s)
        {
            VRC.Tools.ClearCookies();

            if (s == null || !s.HasToken)
            {
                // Back to "logged out" without telling VRChat to end any session.
                ApiCredentials.Clear();
                if (APIUser.IsLoggedIn) APIUser.Logout(); // no token left, so this only clears local state
                return null;
            }

            if (string.IsNullOrEmpty(s.twoFactor))
                ApiCredentials.Set(s.humanName, s.providerUserId, s.provider, s.auth);
            else
                ApiCredentials.Set(s.humanName, s.providerUserId, s.provider, s.auth, s.twoFactor);

            Identity me;
            try
            {
                // Uploads go through VRCApi, which reads the token from ApiCredentials on every request,
                // so this answer is exactly the account an upload would land on.
                me = await VRCApi.Get<Identity>("auth/user", forceRefresh: true);
            }
            catch (Exception)
            {
                return null; // 401 = expired / signed out elsewhere
            }
            if (me == null || string.IsNullOrEmpty(me.id)) return null; // e.g. 2FA needed again

            await RefreshCurrentUser();
            return me;
        }

        /// <summary>True when the SDK's APIUser.CurrentUser is the given user.</summary>
        public static bool SdkUserIs(string userId) => APIUser.CurrentUser != null && APIUser.CurrentUser.id == userId;

        public static string SdkUserName => APIUser.CurrentUser?.displayName;

        private static async Task RefreshCurrentUser()
        {
            var done = new TaskCompletionSource<bool>();
            APIUser.InitialFetchCurrentUser(c => done.TrySetResult(true), c => done.TrySetResult(false));
            await Task.WhenAny(done.Task, Task.Delay(FetchTimeoutMs));
        }

        // ------------------------------------------------------------------
        // Sign-in (same calls and order as the SDK control panel)
        // ------------------------------------------------------------------

        /// <param name="fresh">True for the first attempt (starts from a clean slate). False for the attempt right
        /// after a 2FA code was verified, which must keep the cookies the verification produced.</param>
        public static Task<LoginResult> Login(string loginName, string password, bool fresh)
        {
            var result = new TaskCompletionSource<LoginResult>();
            if (fresh)
            {
                VRC.Tools.ClearCookies();
                ApiCredentials.Clear();
            }

            APIUser.Login(loginName, password,
                c =>
                {
                    var user = c.Model as APIUser;
                    var session = new Snapshot
                    {
                        humanName = user?.username ?? loginName,
                        provider = "vrchat",
                        providerUserId = loginName,
                        auth = Cookie(c.Cookies, "auth") ?? ApiCredentials.GetAuthToken() ?? "",
                        twoFactor = Cookie(c.Cookies, "twoFactorAuth") ?? ApiCredentials.GetTwoFactorAuthToken() ?? ""
                    };
                    result.TrySetResult(new LoginResult
                    {
                        state = session.HasToken ? LoginState.Success : LoginState.Failed,
                        error = session.HasToken ? null : "VRChat did not return a session token.",
                        session = session,
                        identity = user == null ? null : new Identity { id = user.id, displayName = user.displayName, username = user.username }
                    });
                },
                c => result.TrySetResult(new LoginResult { state = LoginState.Failed, error = string.IsNullOrEmpty(c.Error) ? "Login failed." : c.Error }),
                c =>
                {
                    // Needs a 2FA code. Keep the half-authenticated cookie so the code can be verified against it.
                    var auth = Cookie(c.Cookies, "auth");
                    if (auth != null) ApiCredentials.Set(loginName, loginName, "vrchat", auth);

                    var model = c.Model as API2FA;
                    string type = API2FA.TIME_BASED_ONE_TIME_PASSWORD_AUTHENTICATION;
                    if (model?.requiresTwoFactorAuth != null && !model.requiresTwoFactorAuth.Contains(API2FA.TIME_BASED_ONE_TIME_PASSWORD_AUTHENTICATION)
                        && model.requiresTwoFactorAuth.Contains(API2FA.EMAIL_BASED_ONE_TIME_PASSWORD_AUTHENTICATION))
                        type = API2FA.EMAIL_BASED_ONE_TIME_PASSWORD_AUTHENTICATION;

                    result.TrySetResult(new LoginResult { state = LoginState.NeedsCode, codeType = type });
                });

            return result.Task;
        }

        public static Task<bool> VerifyCode(string code, string codeType, string loginName, string password)
        {
            var result = new TaskCompletionSource<bool>();
            APIUser.VerifyTwoFactorAuthCode(code.Trim(), codeType, loginName, password,
                delegate { result.TrySetResult(true); },
                delegate { result.TrySetResult(false); });
            return result.Task;
        }

        public static bool IsEmailCode(string codeType) => codeType == API2FA.EMAIL_BASED_ONE_TIME_PASSWORD_AUTHENTICATION;

        private static string Cookie(Dictionary<string, string> cookies, string name) =>
            cookies != null && cookies.TryGetValue(name, out var v) && !string.IsNullOrEmpty(v) ? v : null;
    }
}
#endif
