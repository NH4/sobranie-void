using Microsoft.AspNetCore.SignalR;

namespace Sobranie.Orchestrator.Hubs;

public sealed class SobranieHub : Hub
{
}

public static class SobranieEvents
{
    public const string ReceiveSpeech = "ReceiveSpeech";
    public const string ReceiveSpeechComplete = "ReceiveSpeechComplete";
    public const string ReceiveChorusReaction = "ReceiveChorusReaction";
    public const string StateChange = "StateChange";
}
