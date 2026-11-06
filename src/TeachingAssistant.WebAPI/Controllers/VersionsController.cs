using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("versions/{versionId:guid}")]
public class VersionsController : ControllerBase
{
    // Source code has no registered type; serve it as UTF-8 text so the browser and the code viewer read it correctly.
    private const string TextContentType = "text/plain; charset=utf-8";

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private readonly IStoredSubmissionService _storedService;
    private readonly IGradingService _gradingService;

    public VersionsController(IStoredSubmissionService storedService, IGradingService gradingService)
    {
        _storedService = storedService;
        _gradingService = gradingService;
    }

    /// <summary>
    /// Sends a stored version and its assignment's instructions to the model and returns the draft review (not saved); can take a minute.
    /// </summary>
    /// <response code="200">Draft review, with the files that were sent and skipped</response>
    /// <response code="400">Nothing to grade, or the assignment has no instructions the model can read</response>
    /// <response code="404">No stored version with this ID, or the assignment or an instruction file was not found in Google</response>
    /// <response code="409">Google account not connected or authorization expired; call POST /auth/google/connect</response>
    /// <response code="502">Google Classroom or Drive returned an error or could not be reached</response>
    /// <response code="503">Model unreachable, returned an error, or declined</response>
    [HttpPost("grade")]
    [Produces("application/json")]
    public async Task<ActionResult<GradingResponse>> GradeAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var result = await _gradingService.GradeVersionAsync(versionId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Returns the files extracted from a stored version (its code folder), sorted by path.
    /// </summary>
    /// <response code="200">Files listed successfully (empty when nothing was extracted)</response>
    /// <response code="404">No stored version with this ID</response>
    [HttpGet("code")]
    [Produces("application/json")]
    public async Task<ActionResult<IReadOnlyList<CodeFileResponse>>> GetCodeAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var files = await _storedService.ListCodeAsync(versionId, cancellationToken);
        return files is null ? NotFound() : Ok(files);
    }

    /// <summary>
    /// Returns one extracted file by its path from the code list: source code as text, other files with their own type.
    /// </summary>
    /// <response code="200">The file's content</response>
    /// <response code="404">No such version or file</response>
    [HttpGet("code/file")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCodeFileAsync(Guid versionId, [FromQuery] string path, CancellationToken cancellationToken)
    {
        var stream = await _storedService.OpenCodeFileAsync(versionId, path, cancellationToken);
        if (stream is null)
        {
            return NotFound();
        }

        return File(stream, ContentTypeFor(path));
    }

    // Images, PDFs and the like keep their type; everything else (source code, unknown or "binary" defaults such as
    // the built-in table's application/octet-stream for .java) is served as text.
    private static string ContentTypeFor(string path)
    {
        return ContentTypes.TryGetContentType(path, out var known)
            && !known.StartsWith("text/", StringComparison.Ordinal)
            && known != "application/octet-stream"
            ? known
            : TextContentType;
    }
}
