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

    public VersionsController(IStoredSubmissionService storedService)
    {
        _storedService = storedService;
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
