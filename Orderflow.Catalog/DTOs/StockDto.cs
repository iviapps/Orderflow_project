namespace Orderflow.Catalog.DTOs;

// Stock DTOs - para operaciones específicas de inventario
public record StockResponse(
    int ProductId,
    string ProductName,
    int QuantityAvailable,
    int QuantityReserved,
    DateTime UpdatedAt)
{
    public int QuantityTotal => QuantityAvailable + QuantityReserved;
}

public record UpdateStockRequest(int Quantity);

public record StockOperationRequest(int Quantity);