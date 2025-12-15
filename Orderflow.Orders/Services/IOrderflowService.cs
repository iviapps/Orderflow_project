using Orderflow.Orders.Data.Entities;
using Orderflow.Orders.DTOs;
using Orderflow.Shared.Common;

namespace Orderflow.Orders.Services;

public interface IOrderService
{
    // User operations
    Task<ServiceResult<IEnumerable<OrderListResponse>>> GetUserOrdersAsync(string userId);
    Task<ServiceResult<OrderResponse>> GetByIdAsync(int id, string userId);
    Task<ServiceResult<OrderResponse>> CreateAsync(string userId, CreateOrderRequest request);
    Task<ServiceResult> CancelAsync(int id, string userId);

    // Admin operations

    // Admin operations - ACTUALIZADO con paginación
    Task<ServiceResult<PaginatedResult<OrderListResponse>>> GetAllAsync(
        OrderStatus? status = null,
        string? userId = null,
        int page = 1,
        int pageSize = 20);
    Task<ServiceResult<OrderResponse>> GetByIdForAdminAsync(int id);
    Task<ServiceResult> UpdateStatusAsync(int id, OrderStatus newStatus);
}
