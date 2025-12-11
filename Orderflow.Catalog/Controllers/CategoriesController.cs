using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Build.Tasks;
using Microsoft.Extensions.Hosting;
using Orderflow.Catalog.DTOs;
using Orderflow.Catalog.Entities;
using Orderflow.Catalog.Services;

namespace Orderflow.Catalog.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await categoryService.GetAllAsync(search, page, pageSize);

        Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
        Response.Headers.Append("X-Page", page.ToString());
        Response.Headers.Append("X-Page-Size", pageSize.ToString());

        return Ok(result.Data);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> GetById(int id)
    {
        var result = await categoryService.GetByIdAsync(id);

        if (!result.Succeeded)
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Category not found",
                $"Category with ID {id} was not found."));

        return Ok(result.Data);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Create([FromBody] CreateCategoryRequest request)
    {
        var result = await categoryService.CreateAsync(request);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase)))
                return Conflict(CreateProblemDetails(
                    StatusCodes.Status409Conflict,
                    "Category already exists",
                    $"A category with name '{request.Name}' already exists."));

            return BadRequest(CreateValidationProblemDetails(result.Errors));
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategoryResponse>> Update(int id, [FromBody] UpdateCategoryRequest request)
    {
        var result = await categoryService.UpdateAsync(id, request);

        if (!result.Succeeded)
            return HandleServiceErrors(result.Errors, id, request.Name);

        return Ok(result.Data);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await categoryService.DeleteAsync(id);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                return NotFound(CreateProblemDetails(
                    StatusCodes.Status404NotFound,
                    "Category not found",
                    $"Category with ID {id} was not found."));

            if (result.Errors.Any(e => e.Contains("products", StringComparison.OrdinalIgnoreCase)))
                return Conflict(CreateProblemDetails(
                    StatusCodes.Status409Conflict,
                    "Category has products",
                    "Cannot delete category with associated products. Remove or reassign products first."));

            return BadRequest(CreateValidationProblemDetails(result.Errors));
        }

        return NoContent();
    }

    #region Private Helpers

    private ActionResult HandleServiceErrors(IEnumerable<string> errors, int id, string? name = null)
    {
        var errorList = errors.ToList();

        if (errorList.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Category not found",
                $"Category with ID {id} was not found."));

        if (errorList.Any(e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase)))
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Category already exists",
                $"A category with name '{name}' already exists."));

        return BadRequest(CreateValidationProblemDetails(errorList));
    }

    private static ProblemDetails CreateProblemDetails(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };

    private static ValidationProblemDetails CreateValidationProblemDetails(IEnumerable<string> errors) => new()
    {
        Title = "One or more validation errors occurred.",
        Errors = { ["General"] = errors.ToArray() }
    };

    #endregion
}
