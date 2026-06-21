using Godot;
using System.Collections.Generic;

public partial class TranslatorService : Node
{
    private static readonly string[] LanguageCodes = { "pt", "en", "es", "fr", "de", "it", "ja", "zh", "ru", "ko" };
    private static readonly string[] LanguageNames = { "Portugu\u00eas", "English", "Espa\u00f1ol", "Fran\u00e7ais", "Deutsch", "Italiano", "\u65e5\u672c\u8a9e", "\u4e2d\u6587", "\u0420\u0443\u0441\u0441\u043a\u0438\u0439", "\ud55c\uad6d\uc5b4" };

    private const string TranslateUrl = "https://api.mymemory.translated.net/get";
    private readonly Dictionary<string, string> _cache = new();

    public string SelectedLanguage { get; set; } = "pt";
    public bool Enabled { get; set; } = true;

    public string[] GetLanguageCodes() => LanguageCodes;
    public string[] GetLanguageNames() => LanguageNames;

    public int GetLanguageIndex(string code)
    {
        for (int i = 0; i < LanguageCodes.Length; i++)
            if (LanguageCodes[i] == code) return i;
        return 0;
    }

    public void Translate(string text, string sourceLang, System.Action<string> onResult)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text) || text.Length > 500)
        {
            onResult?.Invoke("");
            return;
        }

        string cacheKey = $"{sourceLang}:{SelectedLanguage}:{text}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            onResult?.Invoke(cached);
            return;
        }

        string source = string.IsNullOrWhiteSpace(sourceLang) ? "Autodetect" : sourceLang;
        string pair = source + "|" + SelectedLanguage;
        string url = $"{TranslateUrl}?q={System.Uri.EscapeDataString(text)}&langpair={System.Uri.EscapeDataString(pair)}";

        var http = new HttpRequest();
        http.Name = "TranslateReq";
        AddChild(http);
        http.RequestCompleted += (result, code, _headers, data) =>
        {
            if (code == 200 && data.Length > 0)
            {
                string json = data.GetStringFromUtf8();
                var parsed = Json.ParseString(json).AsGodotDictionary();
                var responseData = parsed.GetValueOrDefault("responseData", new Godot.Collections.Dictionary()).AsGodotDictionary();
                string translated = responseData.GetValueOrDefault("translatedText", "").AsString();
                if (!string.IsNullOrEmpty(translated))
                {
                    _cache[cacheKey] = translated;
                    onResult?.Invoke(translated);
                    http.QueueFree();
                    return;
                }
            }
            onResult?.Invoke("");
            http.QueueFree();
        };

        var error = http.Request(url);
        if (error != Error.Ok)
        {
            onResult?.Invoke("");
            http.QueueFree();
        }
    }

    public void DetectLanguage(string text, System.Action<string> onResult)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            onResult?.Invoke("");
            return;
        }

        string pair = "Autodetect|" + SelectedLanguage;
        string url = $"{TranslateUrl}?q={System.Uri.EscapeDataString(text)}&langpair={System.Uri.EscapeDataString(pair)}";

        var http = new HttpRequest();
        http.Name = "DetectReq";
        AddChild(http);
        http.RequestCompleted += (result, code, _headers, data) =>
        {
            if (code == 200 && data.Length > 0)
            {
                string json = data.GetStringFromUtf8();
                var parsed = Json.ParseString(json).AsGodotDictionary();
                var responseData = parsed.GetValueOrDefault("responseData", new Godot.Collections.Dictionary()).AsGodotDictionary();
                string lang = responseData.GetValueOrDefault("detectedLanguage", "").AsString();
                if (!string.IsNullOrEmpty(lang))
                {
                    onResult?.Invoke(lang);
                    http.QueueFree();
                    return;
                }
            }
            onResult?.Invoke("");
            http.QueueFree();
        };

        var error = http.Request(url);
        if (error != Error.Ok)
        {
            onResult?.Invoke("");
            http.QueueFree();
        }
    }
}
