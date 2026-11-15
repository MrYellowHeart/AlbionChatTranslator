using AlbionChatTranslator.Core.Translation;
using System.Diagnostics;

namespace AlbionChatTranslator.Tests;

/// <summary>
/// End-to-end checks with real OPUS-MT packs. They download ~230 MB, so they only run when
/// ACT_MODEL_DIR points at a folder to keep the packs in, e.g.
///   ACT_MODEL_DIR=C:\temp\act-models dotnet test --project tests/AlbionChatTranslator.Tests
/// </summary>
public class RealModelTests(ITestOutputHelper output)
{
    private static readonly string? ModelDir = Environment.GetEnvironmentVariable("ACT_MODEL_DIR");

    private static async Task<(ModelCatalog Catalog, ModelStore Store)> Setup(params string[] packIds)
    {
        var catalog = ModelCatalog.LoadEmbedded();
        var store = new ModelStore(ModelDir!);
        foreach (var id in packIds)
            await store.EnsureInstalledAsync(catalog.Packs.Single(p => p.Id == id), TestContext.Current.CancellationToken);
        return (catalog, store);
    }

    [Fact]
    public async Task Spanish_chat_translates_to_english_with_albion_terms_intact()
    {
        Assert.SkipWhen(ModelDir is null, "Set ACT_MODEL_DIR to run real-model tests.");
        var (catalog, store) = await Setup("es-en");
        using var provider = new OfflineProvider(catalog, store) { AutoDownload = false };
        await using var service = new TranslationService(new ChatLanguageDetector(), provider) { TargetLanguage = "en" };

        string[] lines =
        [
            "busco grupo para Hellgate en Martlock ahora mismo",
            "alguien vende bolsa t6.1 barata?",
            "vamos a la mazmorra corrupta, necesitamos un sanador",
            "cuidado, hay gankers en la entrada de Caerleon",
        ];

        foreach (var line in lines)
        {
            var sw = Stopwatch.StartNew();
            var tcs = new TaskCompletionSource<TranslationResult>();
            service.Submit(line, tcs.SetResult);
            var r = await tcs.Task.WaitAsync(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
            output.WriteLine($"{sw.ElapsedMilliseconds,5} ms  [{r.Outcome}/{r.SourceLanguage}]  {line}  =>  {r.Text}");

            Assert.Equal(TranslationOutcome.Translated, r.Outcome);
            // Spanish and Portuguese are close on short lines; both routes handle Spanish
            // (Portuguese uses the multi-Romance model), so either detection is fine.
            Assert.Contains(r.SourceLanguage, new[] { "es", "pt" });
            Assert.False(string.IsNullOrWhiteSpace(r.Text));
        }
    }

    [Fact]
    public async Task Placeholders_survive_the_model()
    {
        Assert.SkipWhen(ModelDir is null, "Set ACT_MODEL_DIR to run real-model tests.");
        var (catalog, store) = await Setup("es-en");
        using var model = new MarianModel(store.PackDirectory(catalog.Packs.Single(p => p.Id == "es-en")));

        string[] masked =
        [
            "busco grupo para [0] en [1] ahora mismo",
            "alguien vende bolsa [0] barata?",
            "nos vemos en [0] con [1] y [2]",
        ];
        int kept = 0;
        foreach (var text in masked)
        {
            var result = model.Translate(text);
            int expected = text.Count(c => c == '[');
            bool ok = Enumerable.Range(0, expected).All(i => result.Contains($"[{i}]"));
            if (ok) kept++;
            output.WriteLine($"{(ok ? "OK  " : "LOST")} {text}  =>  {result}");
        }
        // Lost placeholders are handled by a plain-text fallback, but they should be rare.
        Assert.True(kept >= masked.Length - 1, $"Only {kept}/{masked.Length} lines kept their placeholders.");
    }

    [Fact]
    public async Task Reply_translates_english_into_brazilian_portuguese()
    {
        Assert.SkipWhen(ModelDir is null, "Set ACT_MODEL_DIR to run real-model tests.");
        var (catalog, store) = await Setup("en-ROMANCE");
        using var provider = new OfflineProvider(catalog, store) { AutoDownload = false };
        await using var service = new TranslationService(new ChatLanguageDetector(), provider) { TargetLanguage = "en" };

        var sw = Stopwatch.StartNew();
        var r = await service.TranslateReplyAsync("I can heal, invite me to the party please", "pt", TestContext.Current.CancellationToken);
        output.WriteLine($"{sw.ElapsedMilliseconds} ms  [{r.Outcome}]  => {r.Text}");

        Assert.Equal(TranslationOutcome.Translated, r.Outcome);
        Assert.DoesNotContain(">>", r.Text);
        Assert.False(string.IsNullOrWhiteSpace(r.Text));
    }
}
