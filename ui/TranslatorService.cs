using Godot;
using System.Collections.Generic;

public partial class TranslatorService : Node
{
    private static readonly string[] LanguageCodes = { "pt", "en", "es", "fr", "de", "it", "ja", "zh", "ru", "ko" };
    private static readonly string[] LanguageNames = { "Portugu\u00eas", "English", "Espa\u00f1ol", "Fran\u00e7ais", "Deutsch", "Italiano", "\u65e5\u672c\u8a9e", "\u4e2d\u6587", "\u0420\u0443\u0441\u0441\u043a\u0438\u0439", "\ud55c\uad6d\uc5b4" };

    private static readonly string[] TranslateUrls =
    {
        "https://libretranslate.com/translate",
        "https://translate.argosopentech.com/translate",
    };

    private int _apiIndex;
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

        var body = new Godot.Collections.Dictionary
        {
            ["q"] = text,
            ["source"] = sourceLang,
            ["target"] = SelectedLanguage,
            ["format"] = "text",
        };

        string jsonBody = Json.Stringify(body);
        var headers = new string[] { "Content-Type: application/json" };

        var http = new HttpRequest();
        http.Name = "TranslateReq";
        AddChild(http);
        http.RequestCompleted += (result, code, _headers, data) =>
        {
            if (code == 200 && data.Length > 0)
            {
                string json = data.GetStringFromUtf8();
                var parsed = Json.ParseString(json).AsGodotDictionary();
                string translated = parsed.GetValueOrDefault("translatedText", "").AsString();
                if (!string.IsNullOrEmpty(translated))
                {
                    _cache[cacheKey] = translated;
                    onResult?.Invoke(translated);
                    http.QueueFree();
                    return;
                }
            }
            _apiIndex = (_apiIndex + 1) % TranslateUrls.Length;
            onResult?.Invoke("");
            http.QueueFree();
        };

        http.Request(TranslateUrls[_apiIndex], headers, HttpClient.Method.Post, jsonBody);
    }

    public void DetectLanguage(string text, System.Action<string> onResult)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            onResult?.Invoke("");
            return;
        }

        var body = new Godot.Collections.Dictionary { ["q"] = text };
        string jsonBody = Json.Stringify(body);
        var headers = new string[] { "Content-Type: application/json" };

        var http = new HttpRequest();
        http.Name = "DetectReq";
        AddChild(http);
        http.RequestCompleted += (result, code, _headers, data) =>
        {
            if (code == 200 && data.Length > 0)
            {
                string json = data.GetStringFromUtf8();
                var arr = Json.ParseString(json).AsGodotArray();
                if (arr.Count > 0)
                {
                    var first = arr[0].AsGodotDictionary();
                    string lang = first.GetValueOrDefault("language", "").AsString();
                    onResult?.Invoke(lang);
                    http.QueueFree();
                    return;
                }
            }
            onResult?.Invoke("");
            http.QueueFree();
        };

        http.Request("https://libretranslate.com/detect", headers, HttpClient.Method.Post, jsonBody);
    }
}
