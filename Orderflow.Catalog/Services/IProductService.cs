using Orderflow.Catalog.DTOs;

namespace Orderflow.Catalog.Services;

public interface IProductService
{
    Task<ServiceResult<IEnumerable<ProductListResponse>>> GetAllAsync(
        int? categoryId = null,
        bool? isActive = null,
        string? search = null,
        int page = 1,
        int pageSize = 20);

    Task<ServiceResult<ProductResponse>> GetByIdAsync(int id);

    Task<ServiceResult<ProductResponse>> CreateAsync(CreateProductRequest request);

    Task<ServiceResult<ProductResponse>> UpdateAsync(int id, UpdateProductRequest request);

    Task<ServiceResult> DeleteAsync(int id);

    Task<ServiceResult> ActivateAsync(int id);

    Task<ServiceResult> DeactivateAsync(int id);
}