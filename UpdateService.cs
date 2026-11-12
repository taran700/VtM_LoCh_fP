using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
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
        private const string ReleaseApi = "https://api.github.com/repos/taran700/VtM_LoCh_fP/releases/latest";
        private const int CheckIntervalMinutes = 15;
        private static readonly HttpClient Client = CreateClient();
        private static int _isChecking;

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VtM_LoCh_fP-Updater/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
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
            if (AppSettings.UpdateRateLimitUntilUtc.HasValue && now < AppSettings.UpdateRateLimitUntilUtc.Value)
            {
                string until = AppSettings.UpdateRateLimitUntilUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
                WriteLog("Проверка заблокирована ограничением GitHub до " + until + ".");
                return new UpdateCheckResult
                {
                    UpdateAvailable = RestoreCachedUpdaterState(),
                    WasCached = true,
                    ReleaseVersion = AppSettings.UpdateLastKnownVersion,
                    Message = "GitHub временно ограничил количество запросов. Следующая проверка будет доступна: " + until
                };
            }

            if (!manual && AppSettings.UpdateLastAttemptUtc.HasValue &&
                now - AppSettings.UpdateLastAttemptUtc.Value < TimeSpan.FromMinutes(CheckIntervalMinutes))
            {
                WriteLog("Использован кеш: с последней попытки прошло менее 15 минут.");
                bool available = RestoreCachedUpdaterState();
                return new UpdateCheckResult
                {
                    UpdateAvailable = available,
                    WasCached = true,
                    ReleaseVersion = AppSettings.UpdateLastKnownVersion,
                    Message = AppSettings.UpdateLastCheckMessage ?? (available ? "Доступна новая версия." : "Проверка обновлений недавно выполнялась.")
                };
            }

            // Фиксируем попытку до сетевого вызова: ошибки сети также получают 15-минутную паузу.
            AppSettings.UpdateLastAttemptUtc = now;
            AppSettings.Save();
            WriteLog("Начата проверка. Текущая версия: " + GetCurrentVersionText() + "; ручная: " + manual + ".");

            try
            {
                using (HttpResponseMessage response = await Client.GetAsync(ReleaseApi).ConfigureAwait(true))
                {
                    int status = (int)response.StatusCode;
                    string remaining = Header(response, "X-RateLimit-Remaining");
                    string reset = Header(response, "X-RateLimit-Reset");
                    WriteLog("GitHub HTTP " + status + "; X-RateLimit-Remaining=" + (remaining ?? "нет") + "; X-RateLimit-Reset=" + (reset ?? "нет") + ".");

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        CompleteAttempt();
                        AppSettings.UpdateLastSuccessUtc = DateTime.UtcNow;
                        AppSettings.UpdateLastKnownVersion = string.Empty;
                        AppSettings.UpdateLastAssetUrl = string.Empty;
                        AppSettings.UpdateLastCheckMessage = "На GitHub пока нет опубликованного релиза для проверки обновлений.";
                        AppSettings.Save();
                        WriteLog("Релиз не найден (404). Опубликованных релизов пока нет.");
                        return new UpdateCheckResult { UpdateAvailable = false, Message = AppSettings.UpdateLastCheckMessage };
                    }

                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        bool rateLimited = string.Equals(remaining, "0", StringComparison.Ordinal) ||
                            body.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            body.IndexOf("API rate limit exceeded", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (rateLimited)
                        {
                            DateTime? resetAt = ParseUnixReset(reset);
                            if (!resetAt.HasValue) resetAt = DateTime.UtcNow.AddMinutes(CheckIntervalMinutes);
                            AppSettings.UpdateLastAttemptUtc = DateTime.UtcNow;
                            DateTime rateLimitRetry = resetAt.Value.AddSeconds(5);
                            DateTime minimumRetry = DateTime.UtcNow.AddMinutes(CheckIntervalMinutes);
                            AppSettings.UpdateRateLimitUntilUtc = rateLimitRetry > minimumRetry ? rateLimitRetry : minimumRetry;
                            AppSettings.UpdateLastCheckMessage = "GitHub временно ограничил количество запросов. Следующая проверка будет доступна: " + AppSettings.UpdateRateLimitUntilUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
                            AppSettings.Save();
                            WriteLog("Обнаружен rate limit 403. Следующая попытка после " + resetAt.Value.ToString("o") + " UTC.");
                            return new UpdateCheckResult { UpdateAvailable = RestoreCachedUpdaterState(), WasCached = false, ReleaseVersion = AppSettings.UpdateLastKnownVersion, Message = AppSettings.UpdateLastCheckMessage };
                        }
                        throw new InvalidOperationException("GitHub вернул 403 Forbidden. Доступ к репозиторию запрещён; это не похоже на превышение лимита запросов.");
                    }

                    if ((int)response.StatusCode == 429)
                    {
                        DateTime retryAt = ParseRetryAfter(response) ?? DateTime.UtcNow.AddMinutes(CheckIntervalMinutes);
                        AppSettings.UpdateLastAttemptUtc = DateTime.UtcNow;
                        DateTime retryAfter = retryAt.AddSeconds(5);
                        DateTime minimumRetry = DateTime.UtcNow.AddMinutes(CheckIntervalMinutes);
                        AppSettings.UpdateRateLimitUntilUtc = retryAfter > minimumRetry ? retryAfter : minimumRetry;
                        AppSettings.UpdateLastCheckMessage = "GitHub временно ограничил количество запросов. Следующая проверка будет доступна: " + AppSettings.UpdateRateLimitUntilUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
                        AppSettings.Save();
                        WriteLog("Получен HTTP 429. Следующая попытка после " + retryAt.ToString("o") + " UTC.");
                        return new UpdateCheckResult { UpdateAvailable = RestoreCachedUpdaterState(), WasCached = false, ReleaseVersion = AppSettings.UpdateLastKnownVersion, Message = AppSettings.UpdateLastCheckMessage };
                    }

                    if ((int)response.StatusCode >= 500 && (int)response.StatusCode <= 599)
                        throw new InvalidOperationException("Сервер GitHub временно недоступен (HTTP " + status + "). Приложение продолжит работу.");
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("GitHub вернул ошибку HTTP " + status + " (" + response.ReasonPhrase + ").");

                    string json = await response.Content.ReadAsStringAsync();
                    JObject release = JObject.Parse(json);
                    string tag = (string)release["tag_name"];
                    if (string.IsNullOrWhiteSpace(tag)) throw new InvalidOperationException("В последнем релизе GitHub отсутствует тег версии.");
                    string normalizedTag = tag.Trim();
                    if (normalizedTag.StartsWith("v", StringComparison.OrdinalIgnoreCase)) normalizedTag = normalizedTag.Substring(1);
                    Version releaseVersion;
                    if (!Version.TryParse(normalizedTag, out releaseVersion)) throw new InvalidOperationException("Тег релиза «" + tag + "» не соответствует формату версии.");

                    string assetName = "VtM_LoCh_fP_v" + releaseVersion + ".zip";
                    JArray assets = release["assets"] as JArray;
                    JObject asset = assets == null ? null : assets.OfType<JObject>().FirstOrDefault(a => string.Equals((string)a["name"], assetName, StringComparison.Ordinal));
                    if (asset == null) throw new InvalidOperationException("В релизе " + tag + " отсутствует обязательный файл «" + assetName + "».");
                    string downloadUrl = (string)asset["browser_download_url"];
                    Uri downloadUri;
                    if (string.IsNullOrWhiteSpace(downloadUrl) || !Uri.TryCreate(downloadUrl, UriKind.Absolute, out downloadUri) ||
                        downloadUri.Scheme != Uri.UriSchemeHttps || !string.Equals(downloadUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("GitHub не предоставил безопасную HTTPS-ссылку на ZIP-архив обновления.");

                    Version currentVersion;
                    if (!Version.TryParse(GetCurrentVersionText(), out currentVersion))
                        throw new InvalidOperationException("Не удалось определить версию установленного приложения.");

                    // Передаём уже полученные данные EZ_Updater. Проверочный HTTP-запрос библиотеки не вызывается.
                    ConfigureEzUpdater(releaseVersion, assetName, downloadUrl, (string)release["name"], (string)release["body"], json);
                    bool available = releaseVersion > currentVersion;

                    CompleteAttempt();
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
                RecordFailure("Превышено время ожидания ответа GitHub. Проверьте интернет-соединение.", ex);
                return new UpdateCheckResult { UpdateAvailable = RestoreCachedUpdaterState(), WasCached = false, ReleaseVersion = AppSettings.UpdateLastKnownVersion, Message = AppSettings.UpdateLastCheckMessage };
            }
            catch (HttpRequestException ex)
            {
                RecordFailure("Не удалось подключиться к GitHub. Проверьте интернет-соединение.", ex);
                return new UpdateCheckResult { UpdateAvailable = RestoreCachedUpdaterState(), WasCached = false, ReleaseVersion = AppSettings.UpdateLastKnownVersion, Message = AppSettings.UpdateLastCheckMessage };
            }
            catch (Exception ex)
            {
                RecordFailure(ex.Message, ex);
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

            // EZ_Updater 0.5.3.1 does not expose a public API for injecting a release.
            // Its Update() calls GitHub again unless API_Executed/APIResponse/AssetSelected
            // have been primed. Adapt only the pinned library's private state, keeping its
            // original ZIP download, extraction, rollback and replacement implementation.
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
            WriteLog("Установка завершена успешно. Автоматическая проверка отложена на 15 минут после установки.");
        }

        private static void CompleteAttempt()
        {
            AppSettings.UpdateLastAttemptUtc = DateTime.UtcNow;
        }

        private static void RecordFailure(string message, Exception exception)
        {
            CompleteAttempt();
            AppSettings.UpdateLastCheckMessage = message;
            // LastKnownVersion/LastAssetUrl/LastSuccessUtc не трогаем: сохраняем последний успешный результат.
            AppSettings.Save();
            WriteLog("Ошибка проверки: " + message + Environment.NewLine + exception);
        }

        private static string GetCurrentVersionText()
        {
            string path = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string version = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion;
            return string.IsNullOrWhiteSpace(version) ? "" : version;
        }

        private static string Header(HttpResponseMessage response, string name)
        {
            IEnumerable<string> values;
            return response.Headers.TryGetValues(name, out values) ? values.FirstOrDefault() : null;
        }

        private static DateTime? ParseUnixReset(string value)
        {
            long seconds;
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds)) return null;
            try { return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime; }
            catch { return null; }
        }

        private static DateTime? ParseRetryAfter(HttpResponseMessage response)
        {
            if (response.Headers.RetryAfter == null) return null;
            if (response.Headers.RetryAfter.Date.HasValue) return response.Headers.RetryAfter.Date.Value.UtcDateTime;
            if (response.Headers.RetryAfter.Delta.HasValue) return DateTime.UtcNow.Add(response.Headers.RetryAfter.Delta.Value);
            return null;
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
            catch { /* Диагностика не должна мешать запуску приложения. */ }
        }
    }
}
