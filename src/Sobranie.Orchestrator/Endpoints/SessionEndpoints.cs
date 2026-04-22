using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sobranie.Infrastructure.Fsm;

namespace Sobranie.Orchestrator.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/session");

        group.MapPost("/start", (SessionState state) =>
        {
            var started = state.Start();
            return Results.Ok(new
            {
                started,
                running = state.IsRunning,
                startedAt = state.StartedAt,
            });
        });

        group.MapPost("/stop", (SessionState state) =>
        {
            var stopped = state.Stop();
            return Results.Ok(new
            {
                stopped,
                running = state.IsRunning,
            });
        });

        group.MapGet("/status", (SessionState state) => Results.Ok(new
        {
            running = state.IsRunning,
            startedAt = state.StartedAt,
            turnsCompleted = state.TurnsCompleted,
            lastError = state.LastError,
        }));

        return app;
    }
}
