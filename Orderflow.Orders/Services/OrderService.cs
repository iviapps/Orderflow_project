using MassTransit;
using Microsoft.EntityFrameworkCore;
using Orderflow.Orders.Data;
using Orderflow.Orders.Data.Entities;
using Orderflow.Orders.DTOs;
using Orderflow.Shared.Common;
using Orderflow.Shared.Events;

namespace Orderflow.Orders.Services;

public class OrderService(
    OrdersDbContext db,
    IHttpClientFactory httpClientFactory,
    IPublishEndpoint publishEndpoint,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<ServiceResult<IEnumerable<OrderListResponse>>> GetUserOrdersAsync(string userId)
    {
        var orders = await db.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderListResponse(
                o.Id, o.Status.ToString(), o.TotalAmount, o.Items.Count, o.CreatedAt))
            .ToListAsync();

        return ServiceResult<IEnumerable<OrderListResponse>>.Success(orders);
    }

    public async Task<ServiceResult<OrderResponse>> GetByIdAsync(int id, string userId)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return ServiceResult<OrderResponse>.Failure("Order not found");
        }

        if (order.UserId != userId)
        {
            return ServiceResult<OrderResponse>.Failure("Access denied");
        }

        return ServiceResult<OrderResponse>.Success(MapToResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> CreateAsync(string userId, CreateOrderRequest request)
    {
        if (!request.Items.Any())
        {
            return ServiceResult<OrderResponse>.Failure("Order must have at least one item");
        }

        var catalogClient = httpClientFactory.CreateClient("catalog");
        var orderItems = new List<OrderItem>();
        var reservedItems = new List<(int ProductId, int Quantity)>();

        foreach (var item in request.Items)
        {
            try
            {
                // Get product info
                var response = await catalogClient.GetAsync($"/api/v1/products/{item.ProductId}");
                if (!response.IsSuccessStatusCode)
                {
                    await ReleaseReservedStockAsync(catalogClient, reservedItems);
                    return ServiceResult<OrderResponse>.Failure($"Product {item.ProductId} not found");
                }

                var product = await response.Content.ReadFromJsonAsync<ProductInfo>();
                if (product is null)
                {
                    await ReleaseReservedStockAsync(catalogClient, reservedItems);
                    return ServiceResult<OrderResponse>.Failure($"Could not fetch product {item.ProductId}");
                }

                if (!product.IsActive)
                {
                    await ReleaseReservedStockAsync(catalogClient, reservedItems);
                    return ServiceResult<OrderResponse>.Failure($"Product {product.Name} is not available");
                }

                // Reserve stock atomically via HTTP
                var reserveResponse = await catalogClient.PostAsJsonAsync(
                    $"/api/v1/products/{item.ProductId}/reserve",
                    new { Quantity = item.Quantity });

                if (!reserveResponse.IsSuccessStatusCode)
                {
                    await ReleaseReservedStockAsync(catalogClient, reservedItems);
                    var error = await reserveResponse.Content.ReadAsStringAsync();
                    return ServiceResult<OrderResponse>.Failure(
                        reserveResponse.StatusCode == System.Net.HttpStatusCode.Conflict
                            ? $"Insufficient stock for {product.Name}"
                            : $"Failed to reserve stock for {product.Name}: {error}");
                }

                reservedItems.Add((item.ProductId, item.Quantity));

                orderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = item.Quantity
                });
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Catalog service unavailable");
                await ReleaseReservedStockAsync(catalogClient, reservedItems);
                return ServiceResult<OrderResponse>.Failure("Catalog service unavailable");
            }
        }

        var order = new Order
        {
            UserId = userId,
            ShippingAddress = request.ShippingAddress,
            Notes = request.Notes,
            Items = orderItems,
            TotalAmount = orderItems.Sum(i => i.UnitPrice * i.Quantity)
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        logger.LogInformation("Order created: {OrderId} for user {UserId}", order.Id, userId);

        // Publish event for audit/notifications (stock already reserved via HTTP)
        var orderCreatedEvent = new OrderCreatedEvent(
            order.Id,
            userId,
            orderItems.Select(i => new OrderItemEvent(i.ProductId, i.ProductName, i.Quantity)));

        await publishEndpoint.Publish(orderCreatedEvent);

        return ServiceResult<OrderResponse>.Success(MapToResponse(order), "Order created successfully");
    }

    private async Task ReleaseReservedStockAsync(HttpClient catalogClient, List<(int ProductId, int Quantity)> reservedItems)
    {
        foreach (var (productId, quantity) in reservedItems)
        {
            try
            {
                await catalogClient.PostAsJsonAsync(
                    $"/api/v1/products/{productId}/release",
                    new { Quantity = quantity });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to release reserved stock for product {ProductId}", productId);
            }
        }
    }

    public async Task<ServiceResult> CancelAsync(int id, string userId)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return ServiceResult.Failure("Order not found");
        }

        if (order.UserId != userId)
        {
            return ServiceResult.Failure("Access denied");
        }

        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
        {
            return ServiceResult.Failure("Order cannot be cancelled at this stage");
        }

        // Release stock via HTTP
        var catalogClient = httpClientFactory.CreateClient("catalog");
        foreach (var item in order.Items)
        {
            try
            {
                var response = await catalogClient.PostAsJsonAsync(
                    $"/api/v1/products/{item.ProductId}/release",
                    new { Quantity = item.Quantity });

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "Failed to release stock for product {ProductId} on order {OrderId} cancellation",
                        item.ProductId, id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error releasing stock for product {ProductId} on order {OrderId} cancellation",
                    item.ProductId, id);
            }
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation("Order cancelled: {OrderId}", id);

        // Publish event for audit/notifications (stock already released via HTTP)
        var orderCancelledEvent = new OrderCancelledEvent(
            order.Id,
            userId,
            order.Items.Select(i => new OrderItemEvent(i.ProductId, i.ProductName, i.Quantity)));

        await publishEndpoint.Publish(orderCancelledEvent);

        return ServiceResult.Success("Order cancelled successfully");
    }

    // Reemplaza SOLO este método en tu OrderService existente
    public async Task<ServiceResult<PaginatedResult<OrderListResponse>>> GetAllAsync(
        OrderStatus? status = null,
        string? userId = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = db.Orders.AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(o => o.UserId == userId);

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderListResponse(
                o.Id, o.Status.ToString(), o.TotalAmount, o.Items.Count, o.CreatedAt))
            .ToListAsync();

        var paginatedResult = PaginatedResult<OrderListResponse>.Create(orders, page, pageSize, totalCount);

        return ServiceResult<PaginatedResult<OrderListResponse>>.Success(paginatedResult);
    }
    public async Task<ServiceResult<OrderResponse>> GetByIdForAdminAsync(int id)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return ServiceResult<OrderResponse>.Failure("Order not found");
        }

        return ServiceResult<OrderResponse>.Success(MapToResponse(order));
    }

    public async Task<ServiceResult> UpdateStatusAsync(int id, OrderStatus newStatus)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null)
        {
            return ServiceResult.Failure("Order not found");
        }

        if (!IsValidStatusTransition(order.Status, newStatus))
        {
            return ServiceResult.Failure($"Cannot transition from {order.Status} to {newStatus}");
        }

        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation("Order status updated: {OrderId} -> {Status}", id, newStatus);

        return ServiceResult.Success("Order status updated successfully");
    }

    private static bool IsValidStatusTransition(OrderStatus current, OrderStatus next)
    {
        return (current, next) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Confirmed, OrderStatus.Processing) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            _ => false
        };
    }

    private static OrderResponse MapToResponse(Order order) => new(
        order.Id,
        order.UserId,
        order.Status.ToString(),
        order.TotalAmount,
        order.ShippingAddress,
        order.Notes,
        order.CreatedAt,
        order.UpdatedAt,
        order.Items.Select(i => new OrderItemResponse(
            i.Id, i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.Subtotal)));

    private record ProductInfo(int Id, string Name, decimal Price, int Stock, bool IsActive);
}
