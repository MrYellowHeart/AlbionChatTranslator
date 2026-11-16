using AlbionChatTranslator.Core.Translation;

namespace AlbionChatTranslator.Tests;

/// <summary>A scripted translator: records every request and answers per line.</summary>
internal sealed class FakeProvider(Func<TranslationItem, ProviderResult> answer, string name = "Fake", bool online = true) : ITranslationProvider
{
    public readonly List<IReadOnlyList<TranslationItem>> Calls = [];

    public FakeProvider(Func<string, (string Text, string Src)> translate, string name = "Fake")
        : this(i => { var (t, s) = translate(i.Text); return new ProviderResult(ProviderOutcome.Translated, t, s, By: name, LeftDevice: true); }, name) { }

    public string Name => name;
    public bool IsOnline => online;

    public List<string> Sent { get { lock (Calls) return Calls.SelectMany(c => c.Select(i => i.Text)).ToList(); } }

    public Task<IReadOnlyList<ProviderResult>> TranslateAsync(IReadOnlyList<TranslationItem> items, string target, CancellationToken ct)
    {
        lock (Calls) Calls.Add(items.ToList());
        return Task.FromResult<IReadOnlyList<ProviderResult>>(items.Select(answer).ToList());
    }

    public void Dispose() { }

    public static ProviderResult Busy(string by = "Fake") => new(ProviderOutcome.Busy, Message: $"{by} busy", By: by, LeftDevice: true);
}

internal static class ServiceHelpers
{
    public static async Task<TranslationResult> Submit(TranslationService svc, string text)
    {
        var tcs = new TaskCompletionSource<TranslationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        svc.Submit(text, tcs.SetResult);
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>Submits several lines at once so they can share a request.</summary>
    public static async Task<TranslationResult[]> SubmitTogether(TranslationService svc, params string[] texts)
    {
        var tasks = texts.Select(t =>
        {
            var tcs = new TaskCompletionSource<TranslationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            svc.Submit(t, tcs.SetResult);
            return tcs.Task;
        }).ToList();
        return await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
    }
}
