using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IInstructionsFetchService
{
    Task<InstructionsFetchResult> FetchAsync(
        string courseId, string courseName, AssignmentResponse assignment, CancellationToken cancellationToken = default);
}
