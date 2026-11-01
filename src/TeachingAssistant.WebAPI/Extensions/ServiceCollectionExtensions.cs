using Anthropic;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Infrastructure.LanguageModel;

namespace TeachingAssistant.WebAPI.Extensions;

public static class ServiceCollectionExtensions
{
    // Everything the API needs, in one call from Program.cs.
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddSwagger();
        services.AddApplicationServices();
        services.AddLanguageModel(configuration);

        return services;
    }

    // Swagger: XML doc comments from WebAPI (controllers) and Application (contracts) show up in the UI.
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Teaching Assistant API", Version = "v1" });
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "TeachingAssistant.WebAPI.xml"));
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "TeachingAssistant.Application.xml"));
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IModelHealthService, ModelHealthService>();

        return services;
    }

    // Language model (Claude). The API key comes from user-secrets or the ANTHROPIC_API_KEY env var.
    public static IServiceCollection AddLanguageModel(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.ApiKey)
                ? new AnthropicClient()
                : new AnthropicClient { ApiKey = options.ApiKey };
        });
        services.AddSingleton<ILanguageModel, ClaudeLanguageModel>();

        return services;
    }
}
