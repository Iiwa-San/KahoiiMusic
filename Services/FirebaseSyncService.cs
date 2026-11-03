using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using KahoiiMusic.Models;

namespace KahoiiMusic.Services
{
    public class CloudMusicPayload
    {
        public List<PlaylistItem> playlists { get; set; } = new List<PlaylistItem>();
        public List<PlaylistItem> savedTracks { get; set; } = new List<PlaylistItem>();
        public List<PlaylistItem> history { get; set; } = new List<PlaylistItem>();
        public List<PlaylistItem> favorites { get; set; } = new List<PlaylistItem>();
        public List<UserPlaylist> collections { get; set; } = new List<UserPlaylist>();
        public string updatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public class FirebaseSyncService
    {
        private static readonly Lazy<FirebaseSyncService> _instance =
            new Lazy<FirebaseSyncService>(() => new FirebaseSyncService());
        public static FirebaseSyncService Instance => _instance.Value;

        private const string ProjectId = "kahoiimusic";
        private const string ApiKey = "AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8";
        private readonly HttpClient _httpClient;
        private readonly JavaScriptSerializer _serializer;

        public bool IsSyncing { get; private set; }
        public string SyncStatusMessage { get; private set; } = "";

        public event EventHandler SyncStatusChanged;

        private System.Threading.Timer _autoUploadTimer;
        private volatile bool _hasPendingAutoUpload = false;
        private readonly object _syncLock = new object();
        private volatile bool _isApplyingCloudPayload = false;
        private volatile bool _isAutoSyncInitialized = false;

        private FirebaseSyncService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        }

        public void InitializeAutoSync()
        {
            if (_isAutoSyncInitialized) return;
            _isAutoSyncInitialized = true;

            FirebaseAuthService.Instance.AuthStateChanged += (s, e) =>
            {
                if (FirebaseAuthService.Instance.IsLoggedIn)
                {
                    Task.Run(() => TriggerAutoFetchAsync());
                }
                else
                {
                    CancelPendingAutoUpload();
                }
            };

            SavedTracksService.Instance.SavedTracksChanged += (s, e) => RequestAutoUpload();
            HistoryService.HistoryChanged += (s, e) => RequestAutoUpload();
            PlaylistCollectionService.Instance.PlaylistsChanged += (s, e) => RequestAutoUpload();

            if (FirebaseAuthService.Instance.IsLoggedIn)
            {
                Task.Run(() => TriggerAutoFetchAsync());
            }
        }

        public void RequestAutoUpload()
        {
            if (_isApplyingCloudPayload) return;
            if (!FirebaseAuthService.Instance.IsLoggedIn) return;

            lock (_syncLock)
            {
                _hasPendingAutoUpload = true;
                if (_autoUploadTimer == null)
                {
                    _autoUploadTimer = new System.Threading.Timer(OnAutoUploadTimerFired, null, 2500, Timeout.Infinite);
                }
                else
                {
                    _autoUploadTimer.Change(2500, Timeout.Infinite);
                }
            }
        }

        private async void OnAutoUploadTimerFired(object state)
        {
            if (_isApplyingCloudPayload || !FirebaseAuthService.Instance.IsLoggedIn) return;
            try
            {
                var payload = GetCurrentLocalPayload();
                await SyncUploadAsync(payload);
            }
            catch { }
        }

        public void CancelPendingAutoUpload()
        {
            lock (_syncLock)
            {
                _hasPendingAutoUpload = false;
                _autoUploadTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        public void FlushSyncOnExit()
        {
            try
            {
                if (!_hasPendingAutoUpload || !FirebaseAuthService.Instance.IsLoggedIn)
                {
                    CancelPendingAutoUpload();
                    return;
                }
                CancelPendingAutoUpload();
                var payload = GetCurrentLocalPayload();
                var task = Task.Run(async () =>
                {
                    await SyncUploadAsync(payload).ConfigureAwait(false);
                });
                task.Wait(500);
            }
            catch { }
        }

        public CloudMusicPayload GetCurrentLocalPayload()
        {
            return new CloudMusicPayload
            {
                savedTracks = SavedTracksService.Instance.GetSavedTracks(),
                history = HistoryService.GetHistory(),
                collections = PlaylistCollectionService.Instance.GetPlaylists()
            };
        }

        public async Task TriggerAutoFetchAsync(Action<int> onDownloadingMissingTracks = null)
        {
            if (!FirebaseAuthService.Instance.IsLoggedIn) return;
            try
            {
                var payload = await SyncFetchAsync();
                if (payload != null)
                {
                    _isApplyingCloudPayload = true;
                    try
                    {
                        ApplyCloudPayload(payload, onDownloadingMissingTracks);
                    }
                    finally
                    {
                        _isApplyingCloudPayload = false;
                    }
                }
                else
                {
                    // Tài khoản mới chưa có tài liệu đám mây: tự động tạo bản lưu khởi tạo đầu tiên
                    var localPayload = GetCurrentLocalPayload();
                    await SyncUploadAsync(localPayload);
                }
            }
            catch { }
        }

        public async Task TriggerManualSyncAsync(Action<int> onDownloadingMissingTracks = null)
        {
            if (!FirebaseAuthService.Instance.IsLoggedIn) return;
            CancelPendingAutoUpload();
            var localPayload = GetCurrentLocalPayload();
            await SyncUploadAsync(localPayload);
            await TriggerAutoFetchAsync(onDownloadingMissingTracks);
        }

        public async Task<CloudMusicPayload> SyncFetchAsync()
        {
            var user = FirebaseAuthService.Instance.CurrentUser;
            if (user == null || string.IsNullOrWhiteSpace(user.localId) || string.IsNullOrWhiteSpace(user.idToken))
            {
                return null;
            }

            IsSyncing = true;
            SyncStatusMessage = "Đang tải dữ liệu từ đám mây...";
            SyncStatusChanged?.Invoke(this, EventArgs.Empty);

            try
            {
                string token = await FirebaseAuthService.Instance.GetValidTokenAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(token))
                {
                    SyncStatusMessage = "Phiên đăng nhập hết hạn";
                    return null;
                }

                string url = string.Format("https://firestore.googleapis.com/v1/projects/{0}/databases/(default)/documents/users/{1}?key={2}",
                    ProjectId, user.localId, ApiKey);

                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(req).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 404)
                    {
                        SyncStatusMessage = "Chưa có dữ liệu trên đám mây";
                    }
                    else if ((int)response.StatusCode == 403)
                    {
                        SyncStatusMessage = "Lỗi 403: Chưa mở quyền (Rules) trong Firestore Database.";
                    }
                    else
                    {
                        SyncStatusMessage = "Tải đám mây thất bại (Mã " + (int)response.StatusCode + ")";
                    }
                    return null;
                }

                string respJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var doc = _serializer.Deserialize<Dictionary<string, object>>(respJson);

                if (doc.ContainsKey("fields") && doc["fields"] is Dictionary<string, object> fields)
                {
                    if (fields.ContainsKey("musicDataJson") && fields["musicDataJson"] is Dictionary<string, object> jsonField)
                    {
                        string stringValue = jsonField.ContainsKey("stringValue") ? jsonField["stringValue"]?.ToString() : null;
                        if (!string.IsNullOrWhiteSpace(stringValue))
                        {
                            var payload = _serializer.Deserialize<CloudMusicPayload>(stringValue);
                            SyncStatusMessage = "Tải đám mây thành công lúc " + DateTime.Now.ToString("HH:mm");
                            return payload;
                        }
                    }
                }

                SyncStatusMessage = "Dữ liệu đám mây rỗng";
                return null;
            }
            catch (Exception ex)
            {
                SyncStatusMessage = "Lỗi tải đám mây: " + ex.Message;
                return null;
            }
            finally
            {
                IsSyncing = false;
                SyncStatusChanged?.Invoke(this, EventArgs.Empty);
                MemoryOptimizer.TrimMemoryAsync(300, aggressive: true);
            }
        }

        public async Task<bool> SyncUploadAsync(CloudMusicPayload payload)
        {
            var user = FirebaseAuthService.Instance.CurrentUser;
            if (user == null || string.IsNullOrWhiteSpace(user.localId) || string.IsNullOrWhiteSpace(user.idToken))
            {
                return false;
            }

            IsSyncing = true;
            SyncStatusMessage = "Đang lưu lên đám mây...";
            SyncStatusChanged?.Invoke(this, EventArgs.Empty);

            try
            {
                string token = await FirebaseAuthService.Instance.GetValidTokenAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(token))
                {
                    SyncStatusMessage = "Phiên đăng nhập hết hạn";
                    return false;
                }

                string url = string.Format("https://firestore.googleapis.com/v1/projects/{0}/databases/(default)/documents/users/{1}?updateMask.fieldPaths=musicDataJson&updateMask.fieldPaths=updatedAt&key={2}",
                    ProjectId, user.localId, ApiKey);

                string musicDataJson = _serializer.Serialize(payload);

                var firestoreBody = new Dictionary<string, object>
                {
                    {
                        "fields", new Dictionary<string, object>
                        {
                            {
                                "musicDataJson", new Dictionary<string, object>
                                {
                                    { "stringValue", musicDataJson }
                                }
                            },
                            {
                                "updatedAt", new Dictionary<string, object>
                                {
                                    { "stringValue", payload.updatedAt }
                                }
                            }
                        }
                    }
                };

                string reqJson = _serializer.Serialize(firestoreBody);
                var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(req).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    SyncStatusMessage = "Đã tự động lưu đám mây lúc " + DateTime.Now.ToString("HH:mm");
                    return true;
                }

                if ((int)response.StatusCode == 403)
                {
                    SyncStatusMessage = "Lỗi 403: Chưa mở quyền (Rules) trong Firestore Database.";
                }
                else if ((int)response.StatusCode == 404)
                {
                    SyncStatusMessage = "Lỗi 404: Chưa tạo Firestore Database trên Firebase Console.";
                }
                else
                {
                    SyncStatusMessage = "Lưu đám mây không thành công (Mã " + (int)response.StatusCode + ")";
                }
                return false;
            }
            catch (Exception ex)
            {
                SyncStatusMessage = "Lỗi lưu đám mây: " + ex.Message;
                return false;
            }
            finally
            {
                IsSyncing = false;
                SyncStatusChanged?.Invoke(this, EventArgs.Empty);
                MemoryOptimizer.TrimMemoryAsync(300, aggressive: true);
            }
        }

        public void ApplyCloudPayload(CloudMusicPayload payload, Action<int> onDownloadingMissingTracks = null)
        {
            if (payload == null) return;

            // 1. Merge History
            if (payload.history != null)
            {
                foreach (var item in payload.history)
                {
                    HistoryService.AddTrack(item);
                }
            }

            // 2. Merge Collections
            if (payload.collections != null)
            {
                var localCollections = PlaylistCollectionService.Instance.GetPlaylists();
                foreach (var col in payload.collections)
                {
                    var existing = localCollections.Find(x => string.Equals(x.Name, col.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        PlaylistCollectionService.Instance.SavePlaylist(col.Name, col.Items ?? new List<PlaylistItem>());
                    }
                    else if (col.Items != null)
                    {
                        foreach (var track in col.Items)
                        {
                            PlaylistCollectionService.Instance.AddTrackToPlaylist(existing.Id, track);
                        }
                    }
                }
            }

            // 3. Auto re-download saved tracks if missing locally
            if (payload.savedTracks != null && payload.savedTracks.Count > 0)
            {
                var missing = new List<PlaylistItem>();
                foreach (var track in payload.savedTracks)
                {
                    if (!SavedTracksService.Instance.IsTrackSaved(track.Id))
                    {
                        missing.Add(track);
                    }
                }

                if (missing.Count > 0)
                {
                    onDownloadingMissingTracks?.Invoke(missing.Count);
                    foreach (var track in missing)
                    {
                        SavedTracksService.Instance.SaveTrackAsync(track);
                    }
                }
            }
        }
    }
}
