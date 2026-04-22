using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Sobranie.Orchestrator.Hubs;

namespace Sobranie.Orchestrator.Endpoints;

public static class SmokeEndpoints
{
    public static IEndpointRouteBuilder MapSmokeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/smoke/ping", () => Results.Ok(new
        {
            status = "ok",
            utc = DateTimeOffset.UtcNow,
        }));

        app.MapGet("/api/smoke/speak", async (
            string? persona,
            string? prompt,
            IChatClient chat,
            IHubContext<SobranieHub> hub,
            CancellationToken ct) =>
        {
            var system = persona ?? "Ти си пратеник во Собранието. Одговараш кратко, на македонски.";
            var user = prompt ?? "Дај кратко мислење за новиот закон за работни односи.";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, system),
                new(ChatRole.User, user),
            };

            var options = new ChatOptions
            {
                Temperature = 0.8f,
                TopP = 0.9f,
                MaxOutputTokens = 180,
            };

            var sw = Stopwatch.StartNew();
            var content = new StringBuilder();
            var tokenCount = 0;

            await foreach (var update in chat.GetStreamingResponseAsync(messages, options, ct))
            {
                var text = update.Text;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                content.Append(text);
                tokenCount++;
                await hub.Clients.All.SendAsync(SobranieEvents.ReceiveSpeech, new
                {
                    chunk = text,
                    done = false,
                }, ct);
            }

            sw.Stop();

            await hub.Clients.All.SendAsync(SobranieEvents.ReceiveSpeech, new
            {
                chunk = string.Empty,
                done = true,
            }, ct);

            return Results.Ok(new
            {
                elapsed_ms = sw.ElapsedMilliseconds,
                tokens_observed = tokenCount,
                content = content.ToString(),
            });
        });

        return app;
    }
}
