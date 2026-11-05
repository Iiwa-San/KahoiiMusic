using System.Drawing;
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
        public string avatarBase64 { get; set; }
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

        private static readonly object _avatarLock = new object();
        public Image UserAvatar { get; private set; }
        public event EventHandler AuthStateChanged;
        public event EventHandler AvatarChanged;
        public static string AvatarFilePath => Path.Combine(AppPaths.SavedDir, "avatar.png");

        public static Bitmap SafeLoadBitmapFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var raw = Image.FromStream(ms))
                {
                    Bitmap copy = new Bitmap(raw.Width, raw.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(copy))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
                    }
                    return copy;
                }
            }
            catch
            {
                return null;
            }
        }

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
            LoadAvatarFromStorage();
        }

        private void LoadAvatarFromStorage()
        {
            try
            {
                byte[] bytes = null;
                if (File.Exists(AvatarFilePath))
                {
                    bytes = File.ReadAllBytes(AvatarFilePath);
                }
                else if (!string.IsNullOrEmpty(CurrentUser?.avatarBase64))
                {
                    try
                    {
                        bytes = Convert.FromBase64String(CurrentUser.avatarBase64);
                        File.WriteAllBytes(AvatarFilePath, bytes);
                    }
                    catch { }
                }

                if (bytes != null && bytes.Length > 0)
                {
                    var newBmp = SafeLoadBitmapFromBytes(bytes);
                    if (newBmp != null)
                    {
                        lock (_avatarLock)
                        {
                            UserAvatar = newBmp;
                        }
                    }
                }
            }
            catch
            {
                lock (_avatarLock)
                {
                    UserAvatar = null;
                }
            }
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

            return await ExecuteAuthRequestAsync(url, body, rememberMe).ConfigureAwait(false);
        }

        public async Task<Tuple<bool, string>> SendPasswordResetEmailAsync(string email)
        {
            email = email?.Trim();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                return Tuple.Create(false, "Vui lòng nhập địa chỉ email hợp lệ.");
            }

            try
            {
                string url = "https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key=" + ApiKey;
                var body = new Dictionary<string, object>
                {
                    { "requestType", "PASSWORD_RESET" },
                    { "email", email }
                };

                string jsonPayload = _serializer.Serialize(body);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content).ConfigureAwait(false);
                string respJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return Tuple.Create(true, "Đã gửi email khôi phục mật khẩu. Vui lòng kiểm tra hộp thư (kể cả mục Spam) của bạn.");
                }

                var dict = _serializer.Deserialize<Dictionary<string, object>>(respJson);
                if (dict != null && dict.ContainsKey("error") && dict["error"] is Dictionary<string, object> errObj)
                {
                    string message = errObj.ContainsKey("message") ? errObj["message"]?.ToString() : "";
                    if (message.Contains("EMAIL_NOT_FOUND"))
                    {
                        return Tuple.Create(false, "Email này chưa được đăng ký tài khoản.");
                    }
                    return Tuple.Create(false, "Lỗi: " + message);
                }

                return Tuple.Create(false, "Gửi email khôi phục thất bại (Mã " + (int)response.StatusCode + ")");
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

            return await ExecuteAuthRequestAsync(url, body, rememberMe).ConfigureAwait(false);
        }

        private async Task<Tuple<bool, string>> ExecuteAuthRequestAsync(string url, Dictionary<string, object> body, bool rememberMe)
        {
            try
            {
                string jsonPayload = _serializer.Serialize(body);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content).ConfigureAwait(false);
                string respJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                var dict = _serializer.Deserialize<Dictionary<string, object>>(respJson);
                if (dict == null)
                {
                    return Tuple.Create(false, "Phản hồi máy chủ không hợp lệ.");
                }

                if (response.IsSuccessStatusCode)
                {
                    string idToken = dict.ContainsKey("idToken") ? dict["idToken"]?.ToString() : "";
                    string email = dict.ContainsKey("email") ? dict["email"]?.ToString() : "";
                    string refreshToken = dict.ContainsKey("refreshToken") ? dict["refreshToken"]?.ToString() : "";
                    string localId = dict.ContainsKey("localId") ? dict["localId"]?.ToString() : "";

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
                bool refreshed = await RefreshTokenAsync().ConfigureAwait(false);
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

                var resp = await _httpClient.SendAsync(req).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    string respJson = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
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

        public void ClearUserDataExceptPlayingTrack(string exceptVideoId = null)
        {
            try
            {
                SavedTracksService.Instance.ClearAllSaved(exceptVideoId);
                HistoryService.ClearHistory(exceptVideoId);
                PlaylistCollectionService.Instance.ClearAllPlaylists();
            }
            catch { }
        }

        public async Task<bool> SaveAndUploadAvatarAsync(Image avatar)
        {
            if (avatar == null) return false;
            try
            {
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    avatar.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    bytes = ms.ToArray();
                }

                AppPaths.EnsureDirectoriesExist();
                File.WriteAllBytes(AvatarFilePath, bytes);

                var newBmp = SafeLoadBitmapFromBytes(bytes);
                lock (_avatarLock)
                {
                    UserAvatar = newBmp;
                }

                string b64 = Convert.ToBase64String(bytes);
                if (CurrentUser != null)
                {
                    CurrentUser.avatarBase64 = b64;
                    SaveSession(CurrentUser);
                }

                AvatarChanged?.Invoke(this, EventArgs.Empty);

                if (!string.IsNullOrEmpty(CurrentUser?.idToken))
                {
                    try
                    {
                        await UploadAvatarBase64Async(b64).ConfigureAwait(false);
                    }
                    catch { }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SaveAvatarFromBase64(string b64)
        {
            if (string.IsNullOrWhiteSpace(b64)) return;
            try
            {
                byte[] bytes = Convert.FromBase64String(b64);
                AppPaths.EnsureDirectoriesExist();
                File.WriteAllBytes(AvatarFilePath, bytes);
                var newBmp = SafeLoadBitmapFromBytes(bytes);
                lock (_avatarLock)
                {
                    UserAvatar = newBmp;
                }
                if (CurrentUser != null)
                {
                    CurrentUser.avatarBase64 = b64;
                    SaveSession(CurrentUser);
                }
                AvatarChanged?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }

        public async Task<bool> UploadAvatarBase64Async(string base64)
        {
            var user = CurrentUser;
            if (user == null || string.IsNullOrWhiteSpace(user.localId)) return false;
            string token = await GetValidTokenAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(token)) return false;

            string url = string.Format("https://firestore.googleapis.com/v1/projects/{0}/databases/(default)/documents/users/{1}?updateMask.fieldPaths=avatarBase64&key={2}",
                "kahoiimusic", user.localId, ApiKey);

            var body = new Dictionary<string, object>
            {
                {
                    "fields", new Dictionary<string, object>
                    {
                        {
                            "avatarBase64", new Dictionary<string, object>
                            {
                                { "stringValue", base64 }
                            }
                        }
                    }
                }
            };

            string reqJson = _serializer.Serialize(body);
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(req).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }

        public void SignOut(string exceptVideoId = null)
        {
            ClearUserDataExceptPlayingTrack(exceptVideoId);
            UserAvatar?.Dispose();
            UserAvatar = null;
            try
            {
                if (File.Exists(AvatarFilePath)) File.Delete(AvatarFilePath);
            }
            catch { }
            SaveSession(null);
            AvatarChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
