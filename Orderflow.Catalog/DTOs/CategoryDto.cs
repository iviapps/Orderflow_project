namespace Orderflow.Catalog.DTOs;

public record CategoryResponse(
    int Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    int ProductCount);

public record CreateCategoryRequest(
    string Name,
    string? Description);

public record UpdateCategoryRequest(
    string Name,
    string? Description);