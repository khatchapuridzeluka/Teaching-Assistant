using System.Text.Json.Serialization;
using Anthropic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Serilog;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Infrastructure.Documents;
using TeachingAssistant.Infrastructure.Google;
using TeachingAssistant.Infrastructure.Jobs;
using TeachingAssistant.Infrastructure.LanguageModel;
using TeachingAssistant.Infrastructure.Persistence;
using TeachingAssistant.Infrastructure.Storage;
using TeachingAssistant.WebAPI.ExceptionHandling;
using TeachingAssistant.WebAPI.Jobs;

namespace TeachingAssistant.WebAPI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSerilogLogging(configuration);
        services.AddErrorHandling();
        services.AddSwagger();
        services.AddApplicationServices();
        services.AddLanguageModel(configuration);
        services.AddGoogle(configuration);
        services.AddPersistence(configuration);
        services.AddJobs(configuration);

        return services;
    }

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
        services.AddScoped<IJobStore, JobStore>();
        services.AddScoped<IAgentChatStore, AgentChatStore>();
        services.AddScoped<IReviewQueueStore, ReviewQueueStore>();
        services.AddScoped<IGradingReportStore, GradingReportStore>();
        services.AddScoped<IStudentMistakeStore, StudentMistakeStore>();
        services.AddScoped<IStudentStrengthStore, StudentStrengthStore>();
        services.AddScoped<IStudentAnalysisStore, StudentAnalysisStore>();
        services.AddScoped<IModelCallStore, ModelCallStore>();
        services.AddScoped<IMistakeAnalysisStore, MistakeAnalysisStore>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<ISnapshotStorage, FileSystemSnapshotStorage>();
        services.AddSingleton<IStudentPhotoStorage, FileSystemPhotoStorage>();
        services.AddSingleton<IDocumentReader, WordDocumentReader>();

        return services;
    }

    public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((sp, logger) => logger
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(sp)
            .Enrich.FromLogContext());

        return services;
    }

    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ApplicationExceptionHandler>();

        return services;
    }

    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Teaching Assistant API", Version = "v1" });
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "TeachingAssistant.WebAPI.xml"));
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "TeachingAssistant.Application.xml"));

            options.SupportNonNullableReferenceTypes();
            options.NonNullableReferenceTypesAsRequired();
        });

        return services;
    }

    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JobOptions>(configuration.GetSection(JobOptions.SectionName));
        services.AddSingleton<JobSignal>();
        services.AddSingleton<IJobSignal>(sp => sp.GetRequiredService<JobSignal>());
        services.AddHostedService<JobWorker>();

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
        services.AddScoped<ClassroomTools>();
        services.AddScoped<StudentTools>();
        services.AddScoped<AgentTools>();
        services.AddScoped<GradingTools>();
        services.AddScoped<AgentRunContext>();
        services.AddScoped<JobReporter>();
        services.AddScoped<IAgentRunWaker, AgentRunWaker>();
        services.AddScoped<IAgentChatService, AgentChatService>();
        services.AddScoped<IAgentRunService, AgentRunService>();
        services.AddScoped<IGradingService, GradingService>();
        services.AddScoped<IGradingDraftService, GradingDraftService>();
        services.AddScoped<IReviewQueueService, ReviewQueueService>();
        services.AddScoped<IGradingReportService, GradingReportService>();
        services.AddScoped<IStudentMistakeService, StudentMistakeService>();
        services.AddScoped<IStudentStrengthService, StudentStrengthService>();
        services.AddScoped<IStudentAnalysisService, StudentAnalysisService>();
        services.AddScoped<IModelUsageService, ModelUsageService>();
        services.AddScoped<IMistakeAnalysisService, MistakeAnalysisService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IJobProcessor, JobProcessor>();
        services.AddScoped<IJobHandler, GradeVersionJobHandler>();
        services.AddScoped<IJobHandler, AnalyzeMistakesJobHandler>();
        services.AddScoped<IJobHandler, AnalyzeStudentJobHandler>();
        services.AddScoped<IJobHandler, FetchStudentWorkJobHandler>();
        services.AddScoped<IJobHandler, AgentRunJobHandler>();
        services.AddScoped<ICourseSyncService, CourseSyncService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IGradebookService, GradebookService>();

        return services;
    }

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
        services.AddSingleton<ClaudeLanguageModel>();
        services.AddScoped<ModelCallContext>();
        services.AddScoped<ILanguageModel>(sp => new RecordingLanguageModel(
            sp.GetRequiredService<ClaudeLanguageModel>(),
            sp.GetRequiredService<IModelCallStore>(),
            sp.GetRequiredService<ModelCallContext>(),
            sp.GetRequiredService<TimeProvider>()));

        return services;
    }

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
