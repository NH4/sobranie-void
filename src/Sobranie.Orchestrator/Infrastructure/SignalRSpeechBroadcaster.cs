using Microsoft.AspNetCore.SignalR;
using Sobranie.Infrastructure.Fsm;
using Sobranie.Orchestrator.Hubs;

namespace Sobranie.Orchestrator.Infrastructure;

/// <summary>
/// SignalR-backed implementation of <see cref="ISpeechBroadcaster"/>. Lives in
/// the Orchestrator project so Infrastructure stays transport-agnostic.
/// Camel-case payload shape is enforced via AddJsonProtocol in Program.cs.
/// </summary>
public sealed class SignalRSpeechBroadcaster(IHubContext<SobranieHub> hub) : ISpeechBroadcaster
{
    public Task BroadcastChunkAsync(string mpId, string chunk, CancellationToken cancellationToken)
        => hub.Clients.All.SendAsync(
            SobranieEvents.ReceiveSpeech,
            new { mpId, chunk, done = false },
            cancellationToken);

    public Task BroadcastSpeechCompleteAsync(SpeechCompletedEvent evt, CancellationToken cancellationToken)
        => hub.Clients.All.SendAsync(SobranieEvents.ReceiveSpeechComplete, evt, cancellationToken);

    public Task BroadcastStateChangeAsync(string state, CancellationToken cancellationToken)
        => hub.Clients.All.SendAsync(SobranieEvents.StateChange, new { state }, cancellationToken);
}
