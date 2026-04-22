using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Sobranie.Infrastructure.Persistence;

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

        return services;
    }
}
