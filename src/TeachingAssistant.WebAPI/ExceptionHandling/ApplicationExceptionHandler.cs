using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Exceptions;

namespace TeachingAssistant.WebAPI.ExceptionHandling;

public class ApplicationExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public ApplicationExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Title)? mapped = exception switch
        {
            InvalidRequestException => (StatusCodes.Status400BadRequest, "Invalid request"),
            LanguageModelUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Model unavailable"),
            SubmissionSourceNotConnectedException => (StatusCodes.Status409Conflict, "Source not connected"),
            SubmissionSourceNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            SubmissionSourceUnavailableException => (StatusCodes.Status502BadGateway, "Source unavailable"),
            _ => null,
        };

        if (mapped is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = mapped.Value.Status;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = mapped.Value.Status,
                Title = mapped.Value.Title,
                Detail = exception.Message,
            },
        });
    }
}
