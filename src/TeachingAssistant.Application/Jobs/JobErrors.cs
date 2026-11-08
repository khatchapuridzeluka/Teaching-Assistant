using TeachingAssistant.Application.Exceptions;

namespace TeachingAssistant.Application.Jobs;

public static class JobErrors
{
    public static bool IsTemporary(Exception exception) => exception is
        LanguageModelUnavailableException
        or SubmissionSourceUnavailableException
        or TimeoutException
        or HttpRequestException;
}
