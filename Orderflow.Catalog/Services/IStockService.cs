using Orderflow.Catalog.DTOs;

namespace Orderflow.Catalog.Services;

public interface IStockService
{
    Task<ServiceResult<StockResponse>> GetByProductIdAsync(int productId);

    Task<ServiceResult<StockResponse>> UpdateStockAsync(int productId, int quantity);

    Task<ServiceResult<StockResponse>> ReserveStockAsync(int productId, int quantity);

    Task<ServiceResult<StockResponse>> ReleaseStockAsync(int productId, int quantity);

    Task<ServiceResult<StockResponse>> AdjustStockAsync(int productId, int adjustment, string reason);
}