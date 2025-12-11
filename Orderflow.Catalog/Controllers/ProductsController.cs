using Microsoft.AspNetCore.Mvc;
using Orderflow.Catalog.DTOs;
using Orderflow.Catalog.Services;

namespace Orderflow.Catalog.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ProductsController(IProductService productService, IStockService stockService) : ControllerBase
{
    #region Product CRUD

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductListResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductListResponse>>> GetAll(
        [FromQuery] int? categoryId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await productService.GetAllAsync(categoryId, isActive, search, page, pageSize);

        Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
        Response.Headers.Append("X-Page", page.ToString());
        Response.Headers.Append("X-Page-Size", pageSize.ToString());

        return Ok(result.Data);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        var result = await productService.GetByIdAsync(id);

        if (!result.Succeeded)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Product not found",
                Detail = $"Product with ID {id} was not found."
            });

        return Ok(result.Data);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] CreateProductRequest request)
    {
        var result = await productService.CreateAsync(request);

        if (!result.Succeeded)
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Validation failed",
                Errors = { ["General"] = result.Errors.ToArray() }
            });

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> Update(int id, [FromBody] UpdateProductRequest request)
    {
        var result = await productService.UpdateAsync(id, request);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Product not found",
                    Detail = $"Product with ID {id} was not found."
                });

            return BadRequest(new ValidationProblemDetails
            {
                Title = "Validation failed",
                Errors = { ["General"] = result.Errors.ToArray() }
            });
        }

        return Ok(result.Data);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await productService.DeleteAsync(id);

        if (!result.Succeeded)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Product not found",
                Detail = $"Product with ID {id} was not found."
            });

        return NoContent();
    }

    #endregion

    #region Stock Operations

    [HttpGet("{id:int}/stock")]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockResponse>> GetStock(int id)
    {
        var result = await stockService.GetByProductIdAsync(id);

        if (!result.Succeeded)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Stock not found",
                Detail = $"Stock for product ID {id} was not found."
            });

        return Ok(result.Data);
    }

    [HttpPatch("{id:int}/stock")]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockResponse>> UpdateStock(int id, [FromBody] UpdateStockRequest request)
    {
        if (request.Quantity < 0)
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Validation failed",
                Errors = { ["Quantity"] = ["Quantity cannot be negative."] }
            });

        var result = await stockService.UpdateStockAsync(id, request.Quantity);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Product not found",
                    Detail = $"Product with ID {id} was not found."
                });

            return BadRequest(new ValidationProblemDetails
            {
                Title = "Validation failed",
                Errors = { ["General"] = result.Errors.ToArray() }
            });
        }

        return Ok(result.Data);
    }

    [HttpPost("{id:int}/stock/reserve")]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockResponse>> ReserveStock(int id, [FromBody] StockOperationRequest request)
    {
        if (request.Quantity <= 0)
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Validation failed",
                Errors = { ["Quantity"] = ["Quantity must be greater than zero."] }
            });

        var result = await stockService.ReserveStockAsync(id, request.Quantity);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Product not found",
                    Detail = $"Product with ID {id} was not found."
                });

            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Insufficient stock",
                Detail = string.Join(" ", result.Errors)
            });
        }

        return Ok(result.Data);
    }

    [HttpPost("{id:int}/stock/release")]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockResponse>> ReleaseStock(int id, [FromBody] StockOperationRequest request)
    {
        if (request.Quantity <= 0)
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Validation failed",
                Errors = { ["Quantity"] = ["Quantity must be greater than zero."] }
            });

        var result = await stockService.ReleaseStockAsync(id, request.Quantity);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Product not found",
                    Detail = $"Product with ID {id} was not found."
                });

            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Stock operation failed",
                Detail = string.Join(" ", result.Errors)
            });
        }

        return Ok(result.Data);
    }

    #endregion
}