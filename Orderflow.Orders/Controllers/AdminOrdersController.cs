using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orderflow.Orders.Data.Entities;
using Orderflow.Orders.DTOs;
using Orderflow.Orders.Services;
using Orderflow.Shared.Common;
namespace Orderflow.Orders.Controllers;

[ApiController]
[Route("api/v1/admin/orders")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<OrderListResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<OrderListResponse>>> GetAll(
    [FromQuery] OrderStatus? status = null,
    [FromQuery] string? userId = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await orderService.GetAllAsync(status, userId, page, pageSize);

        return Ok(result.Data);  // PaginatedResult ya incluye todo
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        var result = await orderService.GetByIdForAdminAsync(id);

        if (!result.Succeeded)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Order not found",
                Detail = $"Order with ID {id} was not found."
            });

        return Ok(result.Data);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var result = await orderService.UpdateStatusAsync(id, request.Status);

        if (!result.Succeeded)
        {
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
                Title = "Cannot update status",
                Detail = string.Join(" ", result.Errors)
            });
        }

        return NoContent();
    }
}
