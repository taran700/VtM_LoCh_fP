using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EZ_Updater;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VtM_LoCh_fP
{
    internal sealed class UpdateCheckResult
    {
        public bool UpdateAvailable { get; set; }
        public bool WasCached { get; set; }
        public string Message { get; set; }
        public string ReleaseVersion { get; set; }
    }

    internal static class UpdateService
    {
        private const string VersionJsonUrl = "https://raw.githubusercontent.com/taran700/VtM_LoCh_fP/refs/heads/master/version.json";
        private const int AutomaticCheckIntervalMinutes = 24 * 60;
        private static readonly HttpClient Client = CreateClient();
        private static int _isChecking;

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VtM_LoCh_fP-Updater/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            return client;
        }

        public static Task<UpdateCheckResult> CheckAsync(bool manual)
        {
            if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0)
                return Task.FromResult(new UpdateCheckResult
                {
                    UpdateAvailable = RestoreCachedUpdaterState(),
                    WasCached = true,
                    ReleaseVersion = AppSettings.UpdateLastKnownVersion,
                    Message = "Проверка обновлений уже выполняется."
                });
            return CheckAndReleaseAsync(manual);
        }

        private static async Task<UpdateCheckResult> CheckAndReleaseAsync(bool manual)
        {
            try { return await CheckCoreAsync(manual).ConfigureAwait(true); }
            finally { Interlocked.Exchange(ref _isChecking, 0); }
        }

        private static async Task<UpdateCheckResult> CheckCoreAsync(bool manual)
        {
            DateTime now = DateTime.UtcNow;
            if (!manual && AppSettings.UpdateLastAttemptUtc.HasValue &&
                now - AppSettings.UpdateLastAttemptUtc.Value < TimeSpan.FromMinutes(AutomaticCheckIntervalMinutes))
            {
                WriteLog("Автоматическая проверка пропущена: 24-часовое КД ещё действует.");
                bool available = RestoreCachedUpdaterState();
                return new UpdateCheckResult
                {
                    UpdateAvailable = available,
                    WasCached = true,
                    ReleaseVersion = AppSettings.UpdateLastKnownVersion,
                    Message = AppSettings.UpdateLastCheckMessage ?? (available ? "Доступна новая версия." : "Автоматическая проверка недавно выполнялась.")
                };
            }

            if (!manual)
            {
                AppSettings.UpdateLastAttemptUtc = now;
                AppSettings.Save();
            }
            WriteLog("Начата проверка через version.json. Текущая версия: " + GetCurrentVersionText() + "; ручная: " + manual + ".");

            try
            {
                using (HttpResponseMessage response = await Client.GetAsync(VersionJsonUrl).ConfigureAwait(true))
                {
                    int status = (int)response.StatusCode;
                    WriteLog("version.json HTTP " + status + ".");
                    if ((int)response.StatusCode == 429)
                        throw new InvalidOperationException("Сервер временно ограничил запросы к version.json (HTTP 429). Попробуйте позже.");
                    if ((int)response.StatusCode >= 500 && (int)response.StatusCode <= 599)
                        throw new InvalidOperationException("Сервер обновлений временно недоступен (HTTP " + status + "). Приложение продолжит работу.");
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Не удалось получить version.json: HTTP " + status + " (" + response.ReasonPhrase + ").");

                    string versionJson = await response.Content.ReadAsStringAsync();
                    JObject versionInfo = JObject.Parse(versionJson);
                    string versionText = (string)versionInfo["version"];
                    if (string.IsNullOrWhiteSpace(versionText))
                        throw new InvalidOperationException("В version.json отсутствует поле version.");
                    string normalizedTag = versionText.Trim();
                    if (normalizedTag.StartsWith("v", StringComparison.OrdinalIgnoreCase)) normalizedTag = normalizedTag.Substring(1);
                    Version releaseVersion;
                    if (!Version.TryParse(normalizedTag, out releaseVersion))
                        throw new InvalidOperationException("Версия «" + versionText + "» в version.json имеет неверный формат.");

                    string assetName = (string)versionInfo["assetName"];
                    if (string.IsNullOrWhiteSpace(assetName)) assetName = "VtM_LoCh_fP_v" + releaseVersion + ".zip";
                    if (!string.Equals(assetName, "VtM_LoCh_fP_v" + releaseVersion + ".zip", StringComparison.Ordinal))
                        throw new InvalidOperationException("Имя ZIP-архива в version.json не соответствует версии.");
                    string downloadUrl = (string)versionInfo["downloadUrl"];
                    if (string.IsNullOrWhiteSpace(downloadUrl))
                        downloadUrl = "https://github.com/taran700/VtM_LoCh_fP/releases/download/v" + releaseVersion + "/" + assetName;
                    Uri downloadUri;
                    if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out downloadUri) ||
                        downloadUri.Scheme != Uri.UriSchemeHttps ||
                        !string.Equals(downloadUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                        !downloadUri.AbsolutePath.Contains("/releases/download/"))
                        throw new InvalidOperationException("В version.json указана небезопасная или некорректная ссылка на ZIP-архив.");

                    JObject release = new JObject
                    {
                        ["tag_name"] = "v" + releaseVersion,
                        ["name"] = (string)versionInfo["releaseName"] ?? ("v" + releaseVersion),
                        ["body"] = (string)versionInfo["releaseNotes"] ?? string.Empty,
                        ["assets"] = new JArray(new JObject
                        {
                            ["name"] = assetName,
                            ["browser_download_url"] = downloadUrl
                        })
                    };
                    string json = release.ToString(Formatting.None);
                    Version currentVersion;
                    if (!Version.TryParse(GetCurrentVersionText(), out currentVersion))
                        throw new InvalidOperationException("Не удалось определить версию установленного приложения.");
                    ConfigureEzUpdater(releaseVersion, assetName, downloadUrl, (string)release["name"], (string)release["body"], json);
                    bool available = releaseVersion > currentVersion;

                    CompleteAttempt(manual);
                    AppSettings.UpdateLastSuccessUtc = DateTime.UtcNow;
                    AppSettings.UpdateLastKnownVersion = releaseVersion.ToString();
                    AppSettings.UpdateLastAssetUrl = downloadUrl;
                    AppSettings.UpdateLastCheckMessage = available ? "Доступна новая версия: " + releaseVersion : "У вас установлена последняя версия приложения.";
                    AppSettings.Save();
                    WriteLog("Проверка завершена успешно. Релиз=" + releaseVersion + "; текущая=" + currentVersion + "; обновление доступно=" + available + "; ZIP=" + assetName + ".");
                    return new UpdateCheckResult { UpdateAvailable = available, ReleaseVersion = releaseVersion.ToString(), Message = AppSettings.UpdateLastCheckMessage };
                }
            }
            catch (TaskCanceledException ex)
            {
                RecordFailure("Превышено время ожидания ответа сервера обновлений. Проверьте интернет-соединение.", ex, manual);
                return new UpdateCheckResult { UpdateAvailable = RestoreCachedUpdaterState(), WasCached = false, ReleaseVersion = AppSettings.UpdateLastKnownVersion, Message = AppSettings.UpdateLastCheckMessage };
            }
            catch (HttpRequestException ex)
            {
                RecordFailure("Не удалось подключиться к серверу обновлений. Проверьте интернет-соединение.", ex, manual);
                return new UpdateCheckResult { UpdateAvailable = RestoreCachedUpdaterState(), WasCached = false, ReleaseVersion = AppSettings.UpdateLastKnownVersion, Message = AppSettings.UpdateLastCheckMessage };
            }
            catch (Exception ex)
            {
                RecordFailure(ex.Message, ex, manual);
                return new UpdateCheckResult { UpdateAvailable = RestoreCachedUpdaterState(), WasCached = false, ReleaseVersion = AppSettings.UpdateLastKnownVersion, Message = AppSettings.UpdateLastCheckMessage };
            }
        }

        public static bool RestoreCachedUpdaterState()
        {
            Version version;
            if (string.IsNullOrWhiteSpace(AppSettings.UpdateLastKnownVersion) ||
                !Version.TryParse(AppSettings.UpdateLastKnownVersion, out version) ||
                string.IsNullOrWhiteSpace(AppSettings.UpdateLastAssetUrl)) return false;
            Uri cachedDownloadUri;
            if (!Uri.TryCreate(AppSettings.UpdateLastAssetUrl, UriKind.Absolute, out cachedDownloadUri) ||
                cachedDownloadUri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(cachedDownloadUri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                ConfigureEzUpdater(version, "VtM_LoCh_fP_v" + version + ".zip", AppSettings.UpdateLastAssetUrl, null, null, null);
                Version current;
                return Version.TryParse(GetCurrentVersionText(), out current) && version > current;
            }
            catch (Exception ex)
            {
                WriteLog("Не удалось восстановить состояние EZ_Updater из кеша: " + ex);
                return false;
            }
        }

        private static void ConfigureEzUpdater(Version version, string assetName, string downloadUrl, string releaseName, string releaseBody, string releaseJson)
        {
            Updater.GitHub_User = "taran700";
            Updater.GitHub_Repository = "VtM_LoCh_fP";
            Updater.OriginalFileName = "VtM_LoCh_fP.exe";
            Updater.GitHub_Asset = assetName;
            Updater.CustomLogger = WriteLog;
            Assembly assembly = typeof(Updater).Assembly;
            if (assembly.GetName().Version != new Version(0, 5, 3, 1))
                throw new InvalidOperationException("Версия EZ_Updater отличается от проверенной 0.5.3.1; передача релиза в установщик отменена.");

            FieldInfo apiResponseField = typeof(Updater).GetField("APIResponse", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo assetSelectedField = typeof(Updater).GetField("AssetSelected", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo apiExecutedField = typeof(Updater).GetField("API_Executed", BindingFlags.NonPublic | BindingFlags.Static);
            if (apiResponseField == null || assetSelectedField == null || apiExecutedField == null)
                throw new InvalidOperationException("В EZ_Updater 0.5.3.1 изменились внутренние поля релиза; безопасная передача полученных данных невозможна.");

            JObject payload;
            if (!string.IsNullOrWhiteSpace(releaseJson))
                payload = JObject.Parse(releaseJson);
            else
                payload = new JObject
                {
                    ["tag_name"] = "v" + version,
                    ["name"] = releaseName ?? ("v" + version),
                    ["body"] = releaseBody ?? string.Empty,
                    ["assets"] = new JArray(new JObject
                    {
                        ["name"] = assetName,
                        ["browser_download_url"] = downloadUrl
                    })
                };

            object apiResponse = JsonConvert.DeserializeObject(payload.ToString(Formatting.None), apiResponseField.FieldType);
            if (apiResponse == null) throw new InvalidOperationException("EZ_Updater не смог разобрать переданные сведения о релизе.");
            apiResponseField.SetValue(null, apiResponse);

            object assetsObject = GetInstanceMember(apiResponse, "Assets");
            System.Collections.IEnumerable assets = assetsObject as System.Collections.IEnumerable;
            object selectedAsset = null;
            if (assets != null)
            {
                foreach (object candidate in assets)
                {
                    object candidateName = GetInstanceMember(candidate, "Name");
                    if (string.Equals(Convert.ToString(candidateName), assetName, StringComparison.Ordinal))
                    {
                        selectedAsset = candidate;
                        break;
                    }
                }
            }
            if (selectedAsset == null)
                throw new InvalidOperationException("Не удалось подготовить ZIP-архив для EZ_Updater.");

            assetSelectedField.SetValue(null, selectedAsset);
            apiExecutedField.SetValue(null, true);
            SetPrivateStaticProperty("ReleaseVersion", version);
            SetPrivateStaticProperty("ReleaseName", (string)payload["name"] ?? ("v" + version));
            SetPrivateStaticProperty("ReleaseBody", (string)payload["body"] ?? string.Empty);
        }

        private static object GetInstanceMember(object instance, string name)
        {
            if (instance == null) return null;
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null) return property.GetValue(instance, null);
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(instance);
        }

        private static void SetPrivateStaticProperty(string name, object value)
        {
            PropertyInfo property = typeof(Updater).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo setter = property == null ? null : property.GetSetMethod(true);
            if (setter == null)
                throw new InvalidOperationException("EZ_Updater не предоставляет ожидаемое свойство " + name + ".");
            setter.Invoke(null, new[] { value });
        }

        public static void RecordInstallCompleted()
        {
            AppSettings.UpdateLastAttemptUtc = DateTime.UtcNow;
            AppSettings.Save();
            WriteLog("Установка завершена успешно. Автоматическая проверка отложена на 24 часа после установки.");
        }

        private static void CompleteAttempt(bool manual)
        {
            if (!manual) AppSettings.UpdateLastAttemptUtc = DateTime.UtcNow;
        }

        private static void RecordFailure(string message, Exception exception, bool manual)
        {
            CompleteAttempt(manual);
            AppSettings.UpdateLastCheckMessage = message;
            AppSettings.Save();
            WriteLog("Ошибка проверки: " + message + Environment.NewLine + exception);
        }

        private static string GetCurrentVersionText()
        {
            string path = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string version = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion;
            return string.IsNullOrWhiteSpace(version) ? "" : version;
        }

        public static void WriteLog(string message)
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VtM_LoCh_fP", "logs");
                Directory.CreateDirectory(directory);
                string safe = Regex.Replace(message ?? string.Empty, @"(?i)(gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+|Bearer\s+[A-Za-z0-9._\-]+)", "[REDACTED]");
                File.AppendAllText(Path.Combine(directory, "updater.log"), "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + safe + Environment.NewLine);
            }
            catch { /* никогда не завершаем приложение при ошибке, приложение ВСЕГДА рабоатет */ }
        }
    }
}
