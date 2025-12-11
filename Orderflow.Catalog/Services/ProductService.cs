using Microsoft.EntityFrameworkCore;
using Orderflow.Catalog.Data;
using Orderflow.Catalog.DTOs;
using Orderflow.Catalog.Entities;

namespace Orderflow.Catalog.Services;

public class ProductService(CatalogDbContext db) : IProductService
{
    public async Task<ServiceResult<IEnumerable<ProductListResponse>>> GetAllAsync(
        int? categoryId = null,
        bool? isActive = null,
        string? search = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = db.Products
            .Include(p => p.Category)
            .Include(p => p.Stock)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Name.Contains(search) ||
                (p.Description != null && p.Description.Contains(search)));

        var totalCount = await query.CountAsync();

        var products = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListResponse(
                p.Id,
                p.Name,
                p.Price,
                p.Stock != null ? p.Stock.QuantityAvailable : 0,
                p.IsActive,
                p.Category != null ? p.Category.Name : "Unknown"))
            .ToListAsync();

        return ServiceResult<IEnumerable<ProductListResponse>>.Success(products, totalCount);
    }

    public async Task<ServiceResult<ProductResponse>> GetByIdAsync(int id)
    {
        var product = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Stock)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return ServiceResult<ProductResponse>.Failure("Product not found");

        return ServiceResult<ProductResponse>.Success(MapToResponse(product));
    }

    public async Task<ServiceResult<ProductResponse>> CreateAsync(CreateProductRequest request)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId))
            return ServiceResult<ProductResponse>.Failure("Category not found");

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            CategoryId = request.CategoryId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Stock = new Stock
            {
                QuantityAvailable = request.InitialStock,
                QuantityReserved = 0,
                UpdatedAt = DateTime.UtcNow
            }
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        await db.Entry(product).Reference(p => p.Category).LoadAsync();

        return ServiceResult<ProductResponse>.Success(MapToResponse(product));
    }

    public async Task<ServiceResult<ProductResponse>> UpdateAsync(int id, UpdateProductRequest request)
    {
        var product = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Stock)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return ServiceResult<ProductResponse>.Failure("Product not found");

        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId))
            return ServiceResult<ProductResponse>.Failure("Category not found");

        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Price = request.Price;
        product.CategoryId = request.CategoryId;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await db.Entry(product).Reference(p => p.Category).LoadAsync();

        return ServiceResult<ProductResponse>.Success(new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Stock?.QuantityAvailable ?? 0,
            product.IsActive,
            product.CategoryId,
            product.Category?.Name ?? "Unknown"));
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var product = await db.Products.FindAsync(id);

        if (product is null)
            return ServiceResult.Failure("Product not found");

        db.Products.Remove(product);
        await db.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ActivateAsync(int id)
    {
        var product = await db.Products.FindAsync(id);

        if (product is null)
            return ServiceResult.Failure("Product not found");

        product.IsActive = true;
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeactivateAsync(int id)
    {
        var product = await db.Products.FindAsync(id);

        if (product is null)
            return ServiceResult.Failure("Product not found");

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ServiceResult.Success();
    }

    private static ProductResponse MapToResponse(Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Stock?.QuantityAvailable ?? 0,
            product.IsActive,
            product.CategoryId,
            product.Category?.Name ?? "Unknown");
    }
}