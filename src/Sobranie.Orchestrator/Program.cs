using System.Globalization;
using System.Text.Json;
using Serilog;
using Sobranie.Infrastructure.DependencyInjection;
using Sobranie.Orchestrator.Endpoints;
using Sobranie.Orchestrator.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File("logs/sobranie-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        formatProvider: CultureInfo.InvariantCulture));

builder.Services.AddSobranieInfrastructure();

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

await app.Services.MigrateSobranieDatabaseAsync().ConfigureAwait(false);

app.UseSerilogRequestLogging();
app.UseCors();

app.MapGet("/", () => Results.Ok(new
{
    service = "sobranie-void orchestrator",
    status = "alive",
    utc = DateTimeOffset.UtcNow,
}));

app.MapGet("/metrics", () => Results.Text(
    """
    # HELP sobranie_up service liveness
    # TYPE sobranie_up gauge
    sobranie_up 1
    """, "text/plain; version=0.0.4"));

app.MapSmokeEndpoints();
app.MapHub<SobranieHub>("/hub");

app.Run();
