using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Dapper;

namespace SevaDesk_App.Services;

public class LanguageItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(NativeName) || NativeName == Name
        ? Name
        : $"{NativeName} ({Name})";

    public override string ToString() => DisplayName;
}

public class LanguagesManifest
{
    public string Version { get; set; } = "1.0";
    public List<LanguageItem> Languages { get; set; } = new();
}

public class LocalizationService
{
    private const string GitHubRawBase = "https://raw.githubusercontent.com/ciizerr/SevaDesk/main/SevaDesk.App/Strings";
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly object _lock = new();

    private readonly string _cacheDirectory;
    private readonly string _bundledDirectory;

    private Dictionary<string, string> _currentStrings = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _fallbackStrings = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<LanguageItem> AvailableLanguages { get; } = new();

    public LanguageItem? CurrentLanguage { get; private set; }

    public string CurrentLanguageCode => CurrentLanguage?.Code ?? "en-US";

    public event EventHandler? LanguageChanged;

    public LocalizationService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(appData, "SevaDesk", "Translations");
        Directory.CreateDirectory(_cacheDirectory);

        // Locate bundled Strings directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidate = Path.Combine(baseDir, "Strings");
        if (Directory.Exists(candidate))
        {
            _bundledDirectory = candidate;
        }
        else
        {
            // Dev environment fallback
            _bundledDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Strings");
        }
    }

    public void Initialize()
    {
        // 1. Copy bundled strings to cache if not present or newer
        SyncBundledFilesToCache();

        // 2. Load languages manifest
        RefreshAvailableLanguages();

        // 3. Load fallback en-US dictionary
        _fallbackStrings = LoadDictionary("en-US");

        // 4. Retrieve saved language preference from DB or default to en-US
        var savedCode = GetSavedLanguageCode() ?? "en-US";
        SetLanguage(savedCode, persist: false);
    }

    private void SyncBundledFilesToCache()
    {
        try
        {
            if (Directory.Exists(_bundledDirectory))
            {
                foreach (var file in Directory.GetFiles(_bundledDirectory, "*.*"))
                {
                    var dest = Path.Combine(_cacheDirectory, Path.GetFileName(file));
                    if (!File.Exists(dest) || File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(dest))
                    {
                        File.Copy(file, dest, overwrite: true);
                    }
                }
            }
        }
        catch
        {
            // Non-fatal, continue with whatever is available
        }
    }

    public void RefreshAvailableLanguages()
    {
        lock (_lock)
        {
            AvailableLanguages.Clear();
            var manifestPath = Path.Combine(_cacheDirectory, "languages.json");
            if (!File.Exists(manifestPath) && Directory.Exists(_bundledDirectory))
            {
                manifestPath = Path.Combine(_bundledDirectory, "languages.json");
            }

            if (File.Exists(manifestPath))
            {
                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize<LanguagesManifest>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (manifest?.Languages != null)
                    {
                        foreach (var lang in manifest.Languages)
                        {
                            AvailableLanguages.Add(lang);
                        }
                    }
                }
                catch
                {
                    // Fallback default
                }
            }

            if (AvailableLanguages.Count == 0)
            {
                AvailableLanguages.Add(new LanguageItem { Code = "en-US", Name = "English (US)", NativeName = "English", File = "en-US.json" });
                AvailableLanguages.Add(new LanguageItem { Code = "hi-IN", Name = "Hindi (India)", NativeName = "हिन्दी", File = "hi-IN.json" });
            }
        }
    }

    private Dictionary<string, string> LoadDictionary(string code)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Find file for this code
        var lang = AvailableLanguages.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
        var fileName = lang?.File ?? $"{code}.json";

        var path = Path.Combine(_cacheDirectory, fileName);
        if (!File.Exists(path) && Directory.Exists(_bundledDirectory))
        {
            path = Path.Combine(_bundledDirectory, fileName);
        }

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (loaded != null)
                {
                    foreach (var kvp in loaded)
                    {
                        dict[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }

        return dict;
    }

    public void SetLanguage(string code, bool persist = true)
    {
        lock (_lock)
        {
            var target = AvailableLanguages.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase))
                         ?? AvailableLanguages.FirstOrDefault(l => string.Equals(l.Code, "en-US", StringComparison.OrdinalIgnoreCase))
                         ?? AvailableLanguages.FirstOrDefault();

            if (target == null) return;

            CurrentLanguage = target;
            _currentStrings = LoadDictionary(target.Code);

            if (persist)
            {
                SaveLanguageCode(target.Code);
            }
        }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key, string? defaultValue = null)
    {
        lock (_lock)
        {
            if (_currentStrings.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }

            if (_fallbackStrings.TryGetValue(key, out var fallback) && !string.IsNullOrWhiteSpace(fallback))
            {
                return fallback;
            }

            return defaultValue ?? key;
        }
    }

    public string GetString(string key, params object[] args)
    {
        var raw = GetString(key);
        try
        {
            return string.Format(raw, args);
        }
        catch
        {
            return raw;
        }
    }

    public string this[string key] => GetString(key);

    public string Format(string key, params object[] args) => GetString(key, args);

    public async Task<(bool success, string message, int newCount)> SyncFromRemoteAsync(bool force = false)
    {
        try
        {
            var manifestUrl = $"{GitHubRawBase}/languages.json?t={DateTime.UtcNow.Ticks}";
            using var manifestResponse = await _httpClient.GetAsync(manifestUrl);
            if (!manifestResponse.IsSuccessStatusCode)
            {
                return (false, $"GitHub connection returned status {manifestResponse.StatusCode}", 0);
            }

            var manifestJson = await manifestResponse.Content.ReadAsStringAsync();
            var remoteManifest = JsonSerializer.Deserialize<LanguagesManifest>(manifestJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (remoteManifest?.Languages == null || remoteManifest.Languages.Count == 0)
            {
                return (false, "No languages found in remote manifest.", 0);
            }

            // Save remote manifest
            var localManifestPath = Path.Combine(_cacheDirectory, "languages.json");
            await File.WriteAllTextAsync(localManifestPath, manifestJson);

            int updatedCount = 0;
            foreach (var lang in remoteManifest.Languages)
            {
                if (string.IsNullOrWhiteSpace(lang.File)) continue;

                var langUrl = $"{GitHubRawBase}/{lang.File}?t={DateTime.UtcNow.Ticks}";
                using var langResp = await _httpClient.GetAsync(langUrl);
                if (langResp.IsSuccessStatusCode)
                {
                    var langJson = await langResp.Content.ReadAsStringAsync();
                    var destPath = Path.Combine(_cacheDirectory, lang.File);
                    await File.WriteAllTextAsync(destPath, langJson);
                    updatedCount++;
                }
            }

            RefreshAvailableLanguages();

            // Refresh current language dictionary if updated
            if (CurrentLanguage != null)
            {
                _currentStrings = LoadDictionary(CurrentLanguage.Code);
                _fallbackStrings = LoadDictionary("en-US");
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }

            return (true, $"Successfully synchronized {updatedCount} language files from GitHub!", updatedCount);
        }
        catch (HttpRequestException)
        {
            return (false, "Could not connect to GitHub. Please check your internet connection.", 0);
        }
        catch (Exception ex)
        {
            return (false, $"Sync error: {ex.Message}", 0);
        }
    }

    private string? GetSavedLanguageCode()
    {
        try
        {
            using var conn = AppServices.Database.CreateConnection();
            conn.Open();
            return conn.QueryFirstOrDefault<string>(
                "SELECT value FROM settings WHERE key = 'app_language';");
        }
        catch
        {
            return null;
        }
    }

    private void SaveLanguageCode(string code)
    {
        try
        {
            using var conn = AppServices.Database.CreateConnection();
            conn.Open();
            conn.Execute(@"
                INSERT INTO settings (key, value) VALUES ('app_language', @code)
                ON CONFLICT(key) DO UPDATE SET value = @code;",
                new { code });
        }
        catch
        {
            // Non-fatal
        }
    }
}
