using Orderflow.Catalog.DTOs;

namespace Orderflow.Catalog.Services;

public interface ICategoryService
{
    Task<ServiceResult<IEnumerable<CategoryResponse>>> GetAllAsync(
        string? search = null,
        int page = 1,
        int pageSize = 20);

    Task<ServiceResult<CategoryResponse>> GetByIdAsync(int id);

    Task<ServiceResult<CategoryResponse>> CreateAsync(CreateCategoryRequest request);

    Task<ServiceResult<CategoryResponse>> UpdateAsync(int id, UpdateCategoryRequest request);

    Task<ServiceResult> DeleteAsync(int id);
}