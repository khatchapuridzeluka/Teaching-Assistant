using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("stored-files")]
public class StoredFilesController : ControllerBase
{
    private readonly IStoredSubmissionService _storedService;

    public StoredFilesController(IStoredSubmissionService storedService)
    {
        _storedService = storedService;
    }

    /// <summary>
    /// Returns a stored submission file by its ID: shown in the browser (images, PDF) or, with download=true, saved under the student's file name.
    /// </summary>
    /// <response code="200">The file's content with its type</response>
    /// <response code="404">No stored file with this ID</response>
    [HttpGet("{contentId:guid}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(Guid contentId, bool download, CancellationToken cancellationToken)
    {
        var file = await _storedService.OpenFileAsync(contentId, cancellationToken);
        if (file is null)
            return NotFound();

        return download
            ? File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true)
            : File(file.Content, file.ContentType, enableRangeProcessing: true);
    }
}
