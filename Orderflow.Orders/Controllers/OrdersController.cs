using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orderflow.Orders.DTOs;
using Orderflow.Orders.Services;
using System.Security.Claims;

namespace Orderflow.Orders.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrderListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<OrderListResponse>>> GetUserOrders()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await orderService.GetUserOrdersAsync(userId);
        return Ok(result.Data);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await orderService.GetByIdAsync(id, userId);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Contains("Access denied", StringComparison.OrdinalIgnoreCase)))
                return Forbid();

            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Order not found",
                Detail = $"Order with ID {id} was not found."
            });
        }

        return Ok(result.Data);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderResponse>> Create([FromBody] CreateOrderRequest request)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await orderService.CreateAsync(userId, request);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Contains("service unavailable", StringComparison.OrdinalIgnoreCase)))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Service unavailable",
                    Detail = "Catalog service is temporarily unavailable. Please try again later."
                });

            return BadRequest(new ValidationProblemDetails
            {
                Title = "Could not create order",
                Errors = { ["General"] = result.Errors.ToArray() }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await orderService.CancelAsync(id, userId);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Contains("Access denied", StringComparison.OrdinalIgnoreCase)))
                return Forbid();

            if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Order not found",
                    Detail = $"Order with ID {id} was not found."
                });

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Cannot cancel order",
                Detail = string.Join(" ", result.Errors)
            });
        }

        return NoContent();
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
}   