using Microsoft.EntityFrameworkCore;
using Orderflow.Catalog.Data;
using Orderflow.Catalog.DTOs;

namespace Orderflow.Catalog.Services;

public class StockService(CatalogDbContext context) : IStockService
{
    public async Task<ServiceResult<StockResponse>> GetByProductIdAsync(int productId)
    {
        var stock = await context.Stocks
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.ProductId == productId);

        if (stock is null)
            return ServiceResult<StockResponse>.Failure("Stock not found for this product.");

        return ServiceResult<StockResponse>.Success(MapToResponse(stock));
    }

    public async Task<ServiceResult<StockResponse>> UpdateStockAsync(int productId, int quantity)
    {
        if (quantity < 0)
            return ServiceResult<StockResponse>.Failure("Quantity cannot be negative.");

        var stock = await context.Stocks
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.ProductId == productId);

        if (stock is null)
            return ServiceResult<StockResponse>.Failure("Product not found.");

        stock.QuantityAvailable = quantity;
        stock.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return ServiceResult<StockResponse>.Success(MapToResponse(stock));
    }

    public async Task<ServiceResult<StockResponse>> ReserveStockAsync(int productId, int quantity)
    {
        if (quantity <= 0)
            return ServiceResult<StockResponse>.Failure("Quantity must be greater than zero.");

        var stock = await context.Stocks
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.ProductId == productId);

        if (stock is null)
            return ServiceResult<StockResponse>.Failure("Product not found.");

        if (stock.QuantityAvailable < quantity)
            return ServiceResult<StockResponse>.Failure(
                $"Insufficient stock. Available: {stock.QuantityAvailable}, Requested: {quantity}");

        stock.QuantityAvailable -= quantity;
        stock.QuantityReserved += quantity;
        stock.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return ServiceResult<StockResponse>.Success(MapToResponse(stock));
    }

    public async Task<ServiceResult<StockResponse>> ReleaseStockAsync(int productId, int quantity)
    {
        if (quantity <= 0)
            return ServiceResult<StockResponse>.Failure("Quantity must be greater than zero.");

        var stock = await context.Stocks
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.ProductId == productId);

        if (stock is null)
            return ServiceResult<StockResponse>.Failure("Product not found.");

        if (stock.QuantityReserved < quantity)
            return ServiceResult<StockResponse>.Failure(
                $"Cannot release more than reserved. Reserved: {stock.QuantityReserved}, Requested: {quantity}");

        stock.QuantityReserved -= quantity;
        stock.QuantityAvailable += quantity;
        stock.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return ServiceResult<StockResponse>.Success(MapToResponse(stock));
    }

    public async Task<ServiceResult<StockResponse>> AdjustStockAsync(int productId, int adjustment, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return ServiceResult<StockResponse>.Failure("Reason is required for stock adjustment.");

        var stock = await context.Stocks
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.ProductId == productId);

        if (stock is null)
            return ServiceResult<StockResponse>.Failure("Product not found.");

        var newQuantity = stock.QuantityAvailable + adjustment;

        if (newQuantity < 0)
            return ServiceResult<StockResponse>.Failure(
                $"Adjustment would result in negative stock. Current: {stock.QuantityAvailable}, Adjustment: {adjustment}");

        stock.QuantityAvailable = newQuantity;
        stock.UpdatedAt = DateTime.UtcNow;

        // TODO: Log adjustment with reason for auditing
        // await context.StockAdjustments.AddAsync(new StockAdjustment { ... });

        await context.SaveChangesAsync();

        return ServiceResult<StockResponse>.Success(MapToResponse(stock));
    }

    #region Private Methods

    private static StockResponse MapToResponse(Entities.Stock stock) => new(
        stock.ProductId,
        stock.Product.Name,
        stock.QuantityAvailable,
        stock.QuantityReserved,
        stock.UpdatedAt);

    #endregion
}