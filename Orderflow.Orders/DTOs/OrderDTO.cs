using Orderflow.Orders.Data.Entities;


namespace Orderflow.Orders.DTOs;

public record OrderResponse(
    int Id,
    string UserId,
    string Status,
    decimal TotalAmount,
    string? ShippingAddress,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IEnumerable<OrderItemResponse> Items);

public record OrderListResponse(
    int Id,
    string Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt);

public record OrderItemResponse(
    int Id,
    int ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);

public record CreateOrderRequest(
    string? ShippingAddress,
    string? Notes,
    IEnumerable<CreateOrderItemRequest> Items);

public record CreateOrderItemRequest(
    int ProductId,
    int Quantity);

public record UpdateOrderStatusRequest(OrderStatus Status);
