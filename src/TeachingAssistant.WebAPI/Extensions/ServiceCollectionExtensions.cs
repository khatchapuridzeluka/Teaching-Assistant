using System.Text.Json.Serialization;
using Anthropic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Serilog;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Infrastructure.Documents;
using TeachingAssistant.Infrastructure.Google;
using TeachingAssistant.Infrastructure.LanguageModel;
using TeachingAssistant.Infrastructure.Persistence;
using TeachingAssistant.Infrastructure.Storage;
using TeachingAssistant.WebAPI.ExceptionHandling;

namespace TeachingAssistant.WebAPI.Extensions;

public static class ServiceCollectionExtensions
{
    // Everything the API needs, in one call from Program.cs.
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Enums travel as names ("Connected"), not numbers, in JSON and Swagger.
        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSerilogLogging(configuration);
        services.AddErrorHandling();
        services.AddSwagger();
        services.AddApplicationServices();
        services.AddLanguageModel(configuration);
        services.AddGoogle(configuration);
        services.AddPersistence(configuration);

        return services;
    }

    // Postgres via EF Core for records, the local disk for submission files.
    // The connection string comes from user-secrets (ConnectionStrings:Postgres).
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("Postgres"))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<ISubmissionStore, SubmissionStore>();
        services.AddScoped<IInstructionsStore, InstructionsStore>();
        services.AddScoped<IStudentStore, StudentStore>();
        services.AddScoped<ICourseAssignmentStore, CourseAssignmentStore>();
        services.AddScoped<IGradingStore, GradingStore>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<ISnapshotStorage, FileSystemSnapshotStorage>();
        services.AddSingleton<IStudentPhotoStorage, FileSystemPhotoStorage>();
        services.AddSingleton<IDocumentReader, WordDocumentReader>();

        return services;
    }

    // Serilog replaces the built-in logger; levels and sinks come from the "Serilog" config section.
    public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((sp, logger) => logger
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(sp)
            .Enrich.FromLogContext());

        return services;
    }

    // Errors are returned as ProblemDetails; known Application exceptions get their own status codes.
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ApplicationExceptionHandler>();

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

            // Non-nullable C# properties become required in the spec, so the generated TypeScript types
            // say "string" vs "string | null" exactly like the contracts.
            options.SupportNonNullableReferenceTypes();
            options.NonNullableReferenceTypesAsRequired();
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IModelHealthService, ModelHealthService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<ISubmissionFetchService, SubmissionFetchService>();
        services.AddScoped<IInstructionsFetchService, InstructionsFetchService>();
        services.AddScoped<IInstructionsService, InstructionsService>();
        services.AddScoped<IStoredSubmissionService, StoredSubmissionService>();
        services.AddScoped<IAgentChatService, AgentChatService>();
        services.AddScoped<IGradingService, GradingService>();
        services.AddScoped<IGradingDraftService, GradingDraftService>();
        services.AddScoped<ICourseSyncService, CourseSyncService>();
        services.AddScoped<IStudentService, StudentService>();

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

    // Google account connection (local "installed app" OAuth) and Classroom as a submission source.
    // Client ID/secret come from user-secrets.
    public static IServiceCollection AddGoogle(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));
        services.AddSingleton<GoogleAuthorization>();
        services.AddSingleton<IGoogleConnectionService, GoogleConnectionService>();
        services.AddSingleton<ISubmissionSource, GoogleClassroomSource>();
        services.AddSingleton<IRemoteFileSource, GoogleDriveFileSource>();
        services.AddHttpClient<IStudentPhotoSource, GooglePhotoSource>(client => client.Timeout = TimeSpan.FromSeconds(15));

        return services;
    }
}
