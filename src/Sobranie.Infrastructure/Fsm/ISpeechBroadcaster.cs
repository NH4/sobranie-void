namespace Sobranie.Infrastructure.Fsm;

public interface ISpeechBroadcaster
{
    Task BroadcastChunkAsync(string mpId, string chunk, CancellationToken cancellationToken);
    Task BroadcastSpeechCompleteAsync(SpeechCompletedEvent evt, CancellationToken cancellationToken);
    Task BroadcastStateChangeAsync(string state, CancellationToken cancellationToken);
}

public sealed record SpeechCompletedEvent(
    long SpeechId,
    string MPId,
    string MPDisplayName,
    string PartyId,
    string Content,
    int TokenCount,
    double ElapsedSeconds,
    DateTimeOffset UtteredAt);
