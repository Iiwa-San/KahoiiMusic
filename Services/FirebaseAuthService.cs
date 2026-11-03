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
                if (user != null)
                {
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

        public async Task<Tuple<bool, string>> SignUpAsync(string email, string password)
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

            return await ExecuteAuthRequestAsync(url, body);
        }

        public async Task<Tuple<bool, string>> SignInAsync(string email, string password)
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

            return await ExecuteAuthRequestAsync(url, body);
        }

        private async Task<Tuple<bool, string>> ExecuteAuthRequestAsync(string url, Dictionary<string, object> body)
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

                    var user = new FirebaseUserInfo
                    {
                        localId = localId,
                        email = email,
                        idToken = idToken
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
        }

        public void SignOut()
        {
            SaveSession(null);
        }
    }
}
