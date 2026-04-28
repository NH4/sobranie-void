using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Sobranie.Domain;
using Sobranie.Infrastructure.Persistence;

namespace Sobranie.Infrastructure.Fsm;

public sealed partial class SpeechGenerator(
    IChatClient chatClient,
    SobranieDbContext db,
    IOptions<SobranieOptions> options)
{
    private const string DefaultCorePrompt =
        "Ти си пратеник во Собранието на Република Северна Македонија. Одговараш кратко на македонски.";

    private readonly SobranieOptions rootOptions = options.Value;
    private readonly OllamaOptions ollama = options.Value.Ollama;

    public async Task<GeneratedSpeech> GenerateAsync(
        MPProfile mp,
        Proposal? currentProposal,
        IReadOnlyList<Speech> recentSpeeches,
        Func<string, Task>? onChunk,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mp);

        var messages = BuildMessages(mp, currentProposal, recentSpeeches, rootOptions.SatireIntensity);
        var promptText = string.Join("\n", messages.Select(m => $"{m.Role}: {GetContent(m)}"));
        var promptHash = ComputeHash(promptText);

        var callLog = new LlmCallLog
        {
            Model = rootOptions.Ollama.Model,
            Purpose = "MainCastSpeech",
            PromptHash = promptHash,
            PromptPreview = promptText.Length > 256 ? promptText[..256] : promptText,
            CalledAt = DateTimeOffset.UtcNow,
        };
        db.LlmCallLogs.Add(callLog);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var chatOptions = new ChatOptions
        {
            Temperature = (float)ollama.Temperature,
            TopP = (float)ollama.TopP,
            MaxOutputTokens = ollama.MaxOutputTokens,
        };

        var sw = Stopwatch.StartNew();
        var content = new StringBuilder();
        var tokens = 0;

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, cancellationToken).ConfigureAwait(false))
        {
            var text = update.Text;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            content.Append(text);
            tokens++;

            if (onChunk is not null)
            {
                await onChunk(text).ConfigureAwait(false);
            }
        }

        sw.Stop();
        var raw = content.ToString();
        var sanitized = Sanitize(raw);
        var wasFiltered = !ReferenceEquals(raw, sanitized) && raw != sanitized;

        callLog.Output = sanitized;
        callLog.OutputTokens = tokens;
        callLog.GenerationSeconds = sw.Elapsed.TotalSeconds;
        callLog.Rejected = wasFiltered;
        callLog.RejectReason = wasFiltered ? "think_tags_stripped" : null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new GeneratedSpeech(
            Content: sanitized,
            TokenCount: tokens,
            ElapsedSeconds: sw.Elapsed.TotalSeconds,
            WasFiltered: wasFiltered);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static List<ChatMessage> BuildMessages(
        MPProfile mp,
        Proposal? currentProposal,
        IReadOnlyList<Speech> recentSpeeches,
        string satireIntensity)
    {
        var sys = new StringBuilder(mp.PersonaCore ?? DefaultCorePrompt);

        var overlay = SelectOverlay(mp, satireIntensity);
        if (!string.IsNullOrWhiteSpace(overlay))
        {
            sys.AppendLine();
            sys.AppendLine();
            sys.Append(overlay);
        }

        if (mp.SignatureMoves.Count > 0)
        {
            sys.AppendLine();
            sys.AppendLine();
            sys.AppendLine("Примери на твојот стил (следи ги, не ги повторувај дословно):");
            foreach (var move in mp.SignatureMoves.Take(3))
            {
                sys.Append("- ");
                sys.AppendLine(move.Exemplar);
            }
        }

        var messages = new List<ChatMessage> { new(ChatRole.System, sys.ToString()) };

        if (recentSpeeches.Count > 0)
        {
            var ctx = new StringBuilder("Последни говори во салата:");
            foreach (var s in recentSpeeches.Take(3).Reverse())
            {
                ctx.AppendLine();
                var speaker = s.MP?.DisplayName ?? s.MPId;
                ctx.Append(speaker).Append(": ").Append(Truncate(s.Content, 200));
            }

            messages.Add(new ChatMessage(ChatRole.User, ctx.ToString()));
            messages.Add(new ChatMessage(ChatRole.Assistant, "Разбирам контекстот."));
        }

        var task = currentProposal is { } prop
            ? $"Во моментов се дебатира: {prop.Headline}. Дај твое мислење (80-150 зборови)."
            : "Дај твое мислење за актуелните политички прашања (80-150 зборови).";

        messages.Add(new ChatMessage(ChatRole.User, task));
        return messages;
    }

    private static string? SelectOverlay(MPProfile mp, string satireIntensity)
        => satireIntensity?.ToLowerInvariant() switch
        {
            "gentle" => mp.PersonaOverlayGentle ?? mp.PersonaOverlaySharp,
            "absurd" => mp.PersonaOverlayAbsurd ?? mp.PersonaOverlaySharp,
            _ => mp.PersonaOverlaySharp,
        };

    private static string GetContent(ChatMessage m) => m.Contents
        .Select(c => c as Microsoft.Extensions.AI.TextContent)
        .Where(c => c is not null)
        .Cast<Microsoft.Extensions.AI.TextContent>()
        .FirstOrDefault()?.Text ?? string.Empty;

    private static string Truncate(string text, int maxLen)
        => text.Length <= maxLen ? text : text[..maxLen] + "…";

    private static string Sanitize(string raw)
    {
        var trimmed = raw.Trim();
        trimmed = StripThinkTags().Replace(trimmed, string.Empty).Trim();
        return trimmed;
    }

    [GeneratedRegex(@"<think>[\s\S]*?</think>", RegexOptions.IgnoreCase)]
    private static partial Regex StripThinkTags();
}

public readonly record struct GeneratedSpeech(
    string Content,
    int TokenCount,
    double ElapsedSeconds,
    bool WasFiltered);
