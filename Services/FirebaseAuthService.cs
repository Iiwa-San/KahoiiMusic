using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace KahoiiMusic.Services
{
    public class FirebaseUserInfo
    {
        public string localId { get; set; }
        public string email { get; set; }
        public string idToken { get; set; }
        public string refreshToken { get; set; }
        public long expiryTimestamp { get; set; }
        public bool rememberMe { get; set; } = true;
    }

    public class FirebaseAuthService
    {
        private static readonly Lazy<FirebaseAuthService> _instance =
            new Lazy<FirebaseAuthService>(() => new FirebaseAuthService());
        public static FirebaseAuthService Instance => _instance.Value;

        private const string ApiKey = "AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8";
        private readonly string _sessionFile;
        private readonly HttpClient _httpClient;
        private readonly JavaScriptSerializer _serializer;

        public FirebaseUserInfo CurrentUser { get; private set; }
        public bool IsLoggedIn => CurrentUser != null && !string.IsNullOrWhiteSpace(CurrentUser.idToken);

        public event EventHandler AuthStateChanged;

        private FirebaseAuthService()
        {
            AppPaths.EnsureDirectoriesExist();
            _sessionFile = Path.Combine(AppPaths.SavedDir, "user_session.json");
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _serializer = new JavaScriptSerializer();
            LoadSession();
        }

        private void LoadSession()
        {
            try
            {
                if (File.Exists(_sessionFile))
                {
                    string json = File.ReadAllText(_sessionFile, Encoding.UTF8);
                    CurrentUser = _serializer.Deserialize<FirebaseUserInfo>(json);
                }
            }
            catch { }
        }

        private void SaveSession(FirebaseUserInfo user)
        {
            CurrentUser = user;
            try
            {
                if (user != null && user.rememberMe)
                {
                    AppPaths.EnsureDirectoriesExist();
                    string json = _serializer.Serialize(user);
                    File.WriteAllText(_sessionFile, json, Encoding.UTF8);
                }
                else
                {
                    if (File.Exists(_sessionFile))
                    {
                        File.Delete(_sessionFile);
                    }
                }
            }
            catch { }
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<Tuple<bool, string>> SignUpAsync(string email, string password, bool rememberMe = true)
        {
            email = email?.Trim();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                return Tuple.Create(false, "Email không hợp lệ hoặc mật khẩu phải từ 6 ký tự trở lên.");
            }

            string url = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + ApiKey;
            var body = new Dictionary<string, object>
            {
                { "email", email },
                { "password", password },
                { "returnSecureToken", true }
            };

            return await ExecuteAuthRequestAsync(url, body, rememberMe);
        }

        public async Task<Tuple<bool, string>> SignInAsync(string email, string password, bool rememberMe = true)
        {
            email = email?.Trim();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Tuple.Create(false, "Vui lòng nhập email và mật khẩu.");
            }

            string url = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=" + ApiKey;
            var body = new Dictionary<string, object>
            {
                { "email", email },
                { "password", password },
                { "returnSecureToken", true }
            };

            return await ExecuteAuthRequestAsync(url, body, rememberMe);
        }

        private async Task<Tuple<bool, string>> ExecuteAuthRequestAsync(string url, Dictionary<string, object> body, bool rememberMe)
        {
            try
            {
                string jsonPayload = _serializer.Serialize(body);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                string respJson = await response.Content.ReadAsStringAsync();

                var dict = _serializer.Deserialize<Dictionary<string, object>>(respJson);

                if (response.IsSuccessStatusCode)
                {
                    string localId = dict.ContainsKey("localId") ? dict["localId"]?.ToString() : "";
                    string email = dict.ContainsKey("email") ? dict["email"]?.ToString() : "";
                    string idToken = dict.ContainsKey("idToken") ? dict["idToken"]?.ToString() : "";
                    string refreshToken = dict.ContainsKey("refreshToken") ? dict["refreshToken"]?.ToString() : "";
                    long expiresIn = 3600;
                    if (dict.ContainsKey("expiresIn") && long.TryParse(dict["expiresIn"]?.ToString(), out long exp))
                    {
                        expiresIn = exp;
                    }
                    long expiryTimestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + expiresIn - 120;

                    var user = new FirebaseUserInfo
                    {
                        localId = localId,
                        email = email,
                        idToken = idToken,
                        refreshToken = refreshToken,
                        expiryTimestamp = expiryTimestamp,
                        rememberMe = rememberMe
                    };
                    SaveSession(user);
                    return Tuple.Create(true, "Thành công");
                }

                if (dict.ContainsKey("error") && dict["error"] is Dictionary<string, object> errObj)
                {
                    string message = errObj.ContainsKey("message") ? errObj["message"]?.ToString() : "Lỗi xác thực";
                    if (message.Contains("EMAIL_EXISTS")) return Tuple.Create(false, "Email này đã được đăng ký.");
                    if (message.Contains("EMAIL_NOT_FOUND") || message.Contains("INVALID_LOGIN_CREDENTIALS") || message.Contains("INVALID_PASSWORD"))
                        return Tuple.Create(false, "Email hoặc mật khẩu không chính xác.");
                    if (message.Contains("CONFIGURATION_NOT_FOUND"))
                        return Tuple.Create(false, "Chưa bật Email/Password Auth trong Firebase Console.");
                    return Tuple.Create(false, "Lỗi: " + message);
                }

                return Tuple.Create(false, "Đăng nhập thất bại (Mã " + (int)response.StatusCode + ")");
            }
            catch (Exception ex)
            {
                return Tuple.Create(false, "Lỗi kết nối: " + ex.Message);
            }
            finally
            {
                MemoryOptimizer.TrimMemoryAsync(300, aggressive: true);
            }
        }

        public async Task<string> GetValidTokenAsync()
        {
            if (CurrentUser == null) return null;

            long now = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            if (now >= CurrentUser.expiryTimestamp && !string.IsNullOrWhiteSpace(CurrentUser.refreshToken))
            {
                bool refreshed = await RefreshTokenAsync();
                if (!refreshed && string.IsNullOrWhiteSpace(CurrentUser.idToken))
                {
                    return null;
                }
            }
            return CurrentUser.idToken;
        }

        public async Task<bool> RefreshTokenAsync()
        {
            if (CurrentUser == null || string.IsNullOrWhiteSpace(CurrentUser.refreshToken))
            {
                return false;
            }

            try
            {
                string url = "https://securetoken.googleapis.com/v1/token?key=" + ApiKey;
                var kv = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", CurrentUser.refreshToken)
                };

                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new FormUrlEncodedContent(kv)
                };

                var resp = await _httpClient.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    string respJson = await resp.Content.ReadAsStringAsync();
                    var dict = _serializer.Deserialize<Dictionary<string, object>>(respJson);

                    if (dict.ContainsKey("id_token"))
                    {
                        CurrentUser.idToken = dict["id_token"]?.ToString();
                        if (dict.ContainsKey("refresh_token"))
                        {
                            CurrentUser.refreshToken = dict["refresh_token"]?.ToString();
                        }
                        long expiresIn = 3600;
                        if (dict.ContainsKey("expires_in") && long.TryParse(dict["expires_in"]?.ToString(), out long exp))
                        {
                            expiresIn = exp;
                        }
                        CurrentUser.expiryTimestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + expiresIn - 120;
                        SaveSession(CurrentUser);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                MemoryOptimizer.TrimMemoryAsync(300, aggressive: true);
            }
        }

        public void SignOut()
        {
            SaveSession(null);
        }
    }
}

