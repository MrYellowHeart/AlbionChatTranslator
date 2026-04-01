using DeepL;

namespace AlbionChatTranslator.Translation;

public class DeepLTranslator : IDisposable
{
    private Translator? _translator;

    public bool IsReady => _translator != null;
    public string? LastError { get; private set; }

    public void SetApiKey(string apiKey)
    {
        _translator?.Dispose();
        _translator = string.IsNullOrWhiteSpace(apiKey) ? null : new Translator(apiKey);
    }

    /// <summary>Minimum character count before attempting translation.</summary>
    public int MinLength { get; set; } = 8;

    /// <summary>DeepL target language code, e.g. "EN-US", "FR", "DE".</summary>
    public string TargetLanguage { get; set; } = LanguageCode.EnglishAmerican;

    /// <summary>
    /// Translates text to TargetLanguage. Returns (translated text, detected source language code).
    /// Returns null if source matches target, text is too short, or translator is not ready.
    /// </summary>
    public async Task<(string Text, string LangCode)?> TranslateAsync(string text)
    {
        LastError = null;
        if (_translator == null)
        {
            LastError = "API key not set — translation disabled.";
            return null;
        }
        if (text.Length < MinLength) return null;

        try
        {
            var options = new TextTranslateOptions
            {
                PreserveFormatting = true,
                TagHandling        = "xml",
            };
            options.IgnoreTags.Add("x");
            var protected_ = AlbionGlossary.Protect(text);

            if (!AlbionGlossary.HasTranslatableContent(protected_)) return null;

#if DEBUG
            await System.IO.File.AppendAllTextAsync(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "deepl_debug.txt"),
                $"[{DateTime.Now:HH:mm:ss}] IN:  {protected_}\n");
#endif

            var result     = await _translator.TranslateTextAsync(protected_, null, TargetLanguage, options);
            var restored   = AlbionGlossary.Restore(result.Text);

#if DEBUG
            await System.IO.File.AppendAllTextAsync(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "deepl_debug.txt"),
                $"[{DateTime.Now:HH:mm:ss}] OUT: {result.Text}\n\n");
#endif

            return (restored, result.DetectedSourceLanguageCode);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Translates plain text to an arbitrary target language (used by the reply composer).
    /// Source language is auto-detected.
    /// </summary>
    public async Task<string?> TranslateToAsync(string text, string targetLangCode)
    {
        LastError = null;
        if (_translator == null) { LastError = "API key not set."; return null; }
        try
        {
            var target = NormalizeTargetLang(targetLangCode);
            var result = await _translator.TranslateTextAsync(
                text, null, target,
                new TextTranslateOptions { PreserveFormatting = true });
            return result.Text;
        }
        catch (Exception ex) { LastError = ex.Message; return null; }
    }

    // DeepL requires regional variants for some languages; map base codes to a sensible default.
    private static string NormalizeTargetLang(string code) =>
        code.ToUpperInvariant() switch
        {
            "EN" => LanguageCode.EnglishAmerican,
            "PT" => LanguageCode.PortugueseBrazilian,
            "ZH" => LanguageCode.Chinese,
            var c => c,
        };

    public void Dispose() => _translator?.Dispose();
}
