using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Sobranie.Infrastructure.Fsm;
using Sobranie.Infrastructure.Persistence;
using Sobranie.Infrastructure.Scraping;
using Sobranie.Infrastructure.Seeding;

namespace Sobranie.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSobranieInfrastructure(
        this IServiceCollection services,
        Action<SobranieOptions>? configure = null)
    {
        services.AddOptions<SobranieOptions>()
                .BindConfiguration(SobranieOptions.SectionName)
                .PostConfigure(opts =>
                {
                    if (string.IsNullOrWhiteSpace(opts.DatabasePath))
                    {
                        opts.DatabasePath = "sobranie.db";
                    }
                })
                .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddDbContext<SobranieDbContext>((sp, builder) =>
        {
            var opts = sp.GetRequiredService<IOptions<SobranieOptions>>().Value;
            builder.UseSqlite($"Data Source={opts.DatabasePath};Pooling=True;Cache=Shared");
        });

        services.AddSingleton<IChatClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<SobranieOptions>>().Value;
            var client = new OllamaApiClient(new Uri(opts.Ollama.Endpoint), opts.Ollama.Model);
            return client;
        });

        services.AddScoped<SobranieDataSeeder>();

        services.AddSingleton<SessionState>();
        services.AddScoped<UtilityCalculator>();
        services.AddScoped<SpeakerSelector>();
        services.AddScoped<SpeechGenerator>();
        services.AddHostedService<SessionOrchestrator>();
        services.AddHostedService<ChorusEmitterService>();
        services.AddHostedService<RssScraperService>();

        return services;
    }

    public static async Task MigrateSobranieDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SobranieDbContext>();
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        var seeder = scope.ServiceProvider.GetRequiredService<SobranieDataSeeder>();
        await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
    }
}
