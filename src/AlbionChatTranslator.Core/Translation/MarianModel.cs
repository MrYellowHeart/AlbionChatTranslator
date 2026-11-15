using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using System.Text;
using System.Text.Json;

namespace AlbionChatTranslator.Core.Translation;

/// <summary>
/// Runs one OPUS-MT (Marian) translation model locally with ONNX Runtime on the CPU.
/// Greedy decoding without a KV cache: chat lines are short, so this stays fast and simple.
/// </summary>
public sealed class MarianModel : IDisposable
{
    private const int MaxInputTokens = 256;
    private const char SentencePieceSpace = '▁';

    private readonly InferenceSession _encoder;
    private readonly InferenceSession _decoder;
    private readonly SentencePieceTokenizer _sourceTokenizer;
    private readonly Dictionary<string, int> _vocab;
    private readonly string[] _idToToken;
    private readonly int _eosId, _padId, _unkId, _decoderStartId;
    private readonly Lock _runLock = new();

    public MarianModel(string directory, int threads = 2)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            IntraOpNumThreads = threads,
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };
        _encoder = new InferenceSession(Path.Combine(directory, "onnx", "encoder_model_quantized.onnx"), options);
        _decoder = new InferenceSession(Path.Combine(directory, "onnx", "decoder_model_quantized.onnx"), options);

        using (var spm = File.OpenRead(Path.Combine(directory, "source.spm")))
            _sourceTokenizer = SentencePieceTokenizer.Create(spm, false, false);

        _vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(Path.Combine(directory, "vocab.json")))
            ?? throw new InvalidDataException("vocab.json is empty.");
        _idToToken = new string[_vocab.Values.Max() + 1];
        foreach (var (token, id) in _vocab) _idToToken[id] = token;

        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "config.json")));
        var root = config.RootElement;
        _eosId = root.GetProperty("eos_token_id").GetInt32();
        _padId = root.GetProperty("pad_token_id").GetInt32();
        _decoderStartId = root.TryGetProperty("decoder_start_token_id", out var ds) ? ds.GetInt32() : _padId;
        _unkId = _vocab.TryGetValue("<unk>", out var unk) ? unk : 1;
    }

    /// <param name="targetPrefix">Target-language token for multi-target models, e.g. "&gt;&gt;pt_br&lt;&lt;".</param>
    public string Translate(string text, string? targetPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var inputIds = Encode(text, targetPrefix);
        lock (_runLock)
        {
            var hidden = RunEncoder(inputIds, out var mask);
            var output = GreedyDecode(hidden, mask, maxNewTokens: Math.Min(2 * inputIds.Count + 16, 256));
            return Detokenize(output);
        }
    }

    private List<long> Encode(string text, string? targetPrefix)
    {
        var ids = new List<long>(64);
        if (targetPrefix is not null && _vocab.TryGetValue(targetPrefix, out int prefixId))
            ids.Add(prefixId);

        foreach (var token in _sourceTokenizer.EncodeToTokens(text, out _, false, false))
        {
            ids.Add(_vocab.TryGetValue(token.Value, out int id) ? id : _unkId);
            if (ids.Count >= MaxInputTokens - 1) break;
        }
        ids.Add(_eosId);
        return ids;
    }

    private DenseTensor<float> RunEncoder(List<long> inputIds, out DenseTensor<long> mask)
    {
        int n = inputIds.Count;
        var ids = new DenseTensor<long>(inputIds.ToArray(), [1, n]);
        mask = new DenseTensor<long>(Enumerable.Repeat(1L, n).ToArray(), [1, n]);

        using var results = _encoder.Run(
        [
            NamedOnnxValue.CreateFromTensor("input_ids", ids),
            NamedOnnxValue.CreateFromTensor("attention_mask", mask),
        ]);
        var hidden = results.First().AsTensor<float>();
        return new DenseTensor<float>(hidden.ToArray(), hidden.Dimensions.ToArray());
    }

    private List<int> GreedyDecode(DenseTensor<float> encoderHidden, DenseTensor<long> encoderMask, int maxNewTokens)
    {
        var tokens = new List<long> { _decoderStartId };
        var output = new List<int>();

        for (int step = 0; step < maxNewTokens; step++)
        {
            var decoderIds = new DenseTensor<long>(tokens.ToArray(), [1, tokens.Count]);
            using var results = _decoder.Run(
            [
                NamedOnnxValue.CreateFromTensor("input_ids", decoderIds),
                NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderHidden),
                NamedOnnxValue.CreateFromTensor("encoder_attention_mask", encoderMask),
            ]);

            var logits = results.First().AsTensor<float>(); // [1, seq, vocab]
            int vocab = logits.Dimensions[2];
            int last = tokens.Count - 1;

            int best = -1;
            float bestScore = float.NegativeInfinity;
            for (int v = 0; v < vocab; v++)
            {
                if (v == _padId) continue; // generation_config bans the pad token
                float score = logits[0, last, v];
                if (score > bestScore) { bestScore = score; best = v; }
            }

            if (best < 0 || best == _eosId) break;
            if (IsStuck(output, best)) break;
            output.Add(best);
            tokens.Add(best);
        }
        return output;
    }

    // Guard against the occasional degenerate "the the the the ..." loop on odd input.
    private static bool IsStuck(List<int> output, int next)
    {
        const int maxRepeat = 4;
        if (output.Count < maxRepeat) return false;
        for (int i = output.Count - maxRepeat; i < output.Count; i++)
            if (output[i] != next) return false;
        return true;
    }

    private string Detokenize(List<int> ids)
    {
        var sb = new StringBuilder();
        foreach (int id in ids)
        {
            if (id == _eosId || id == _padId || id == _unkId || id >= _idToToken.Length) continue;
            var token = _idToToken[id];
            if (token is null || (token.StartsWith("<", StringComparison.Ordinal) && token.EndsWith(">", StringComparison.Ordinal))) continue;
            sb.Append(token);
        }
        return sb.Replace(SentencePieceSpace, ' ').ToString().Trim();
    }

    public void Dispose()
    {
        _encoder.Dispose();
        _decoder.Dispose();
    }
}
