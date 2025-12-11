namespace Orderflow.Catalog.DTOs;

// Product DTOs - usar "QuantityAvailable" en lugar de "Stock"
public record ProductResponse(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    int QuantityAvailable,
    bool IsActive,
    int CategoryId,
    string CategoryName);

public record ProductListResponse(
    int Id,
    string Name,
    decimal Price,
    int QuantityAvailable,
    bool IsActive,
    string CategoryName);

public record CreateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    int InitialStock,
    int CategoryId);

public record UpdateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    bool IsActive,
    int CategoryId);