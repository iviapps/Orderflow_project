using Microsoft.EntityFrameworkCore;
using Orderflow.Catalog.Data;
using Orderflow.Catalog.DTOs;
using Orderflow.Catalog.Entities;

namespace Orderflow.Catalog.Services;

public class CategoryService(CatalogDbContext context) : ICategoryService
{
    public async Task<ServiceResult<IEnumerable<CategoryResponse>>> GetAllAsync(
        string? search = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = context.Categories.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(searchLower) ||
                (c.Description != null && c.Description.ToLower().Contains(searchLower)));
        }

        var totalCount = await query.CountAsync();

        var categories = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoryResponse(
                c.Id,
                c.Name,
                c.Description,
                c.CreatedAt,
                c.Products.Count))
            .ToListAsync();

        return ServiceResult<IEnumerable<CategoryResponse>>.Success(categories, totalCount);
    }

    public async Task<ServiceResult<CategoryResponse>> GetByIdAsync(int id)
    {
        var category = await context.Categories
            .Where(c => c.Id == id)
            .Select(c => new CategoryResponse(
                c.Id,
                c.Name,
                c.Description,
                c.CreatedAt,
                c.Products.Count))
            .FirstOrDefaultAsync();

        if (category is null)
            return ServiceResult<CategoryResponse>.Failure("Category not found.");

        return ServiceResult<CategoryResponse>.Success(category);
    }

    public async Task<ServiceResult<CategoryResponse>> CreateAsync(CreateCategoryRequest request)
    {
        // Validation
        var errors = ValidateCategoryRequest(request.Name, request.Description);
        if (errors.Count > 0)
            return ServiceResult<CategoryResponse>.Failure(errors);

        // Check duplicate name
        var exists = await context.Categories
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower());

        if (exists)
            return ServiceResult<CategoryResponse>.Failure("A category with this name already exists.");

        var category = new Category
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var response = new CategoryResponse(
            category.Id,
            category.Name,
            category.Description,
            category.CreatedAt,
            0);

        return ServiceResult<CategoryResponse>.Success(response);
    }

    public async Task<ServiceResult<CategoryResponse>> UpdateAsync(int id, UpdateCategoryRequest request)
    {
        // Validation
        var errors = ValidateCategoryRequest(request.Name, request.Description);
        if (errors.Count > 0)
            return ServiceResult<CategoryResponse>.Failure(errors);

        var category = await context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category is null)
            return ServiceResult<CategoryResponse>.Failure("Category not found.");

        // Check duplicate name (excluding current)
        var duplicateExists = await context.Categories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == request.Name.ToLower());

        if (duplicateExists)
            return ServiceResult<CategoryResponse>.Failure("A category with this name already exists.");

        category.Name = request.Name.Trim();
        category.Description = request.Description?.Trim();

        await context.SaveChangesAsync();

        var response = new CategoryResponse(
            category.Id,
            category.Name,
            category.Description,
            category.CreatedAt,
            category.Products.Count);

        return ServiceResult<CategoryResponse>.Success(response);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var category = await context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category is null)
            return ServiceResult.Failure("Category not found.");

        if (category.Products.Count > 0)
            return ServiceResult.Failure($"Cannot delete category with {category.Products.Count} associated products.");

        context.Categories.Remove(category);
        await context.SaveChangesAsync();

        return ServiceResult.Success();
    }

    #region Private Methods

    private static List<string> ValidateCategoryRequest(string name, string? description)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add("Name is required.");
        else if (name.Length > 100)
            errors.Add("Name cannot exceed 100 characters.");

        if (description?.Length > 500)
            errors.Add("Description cannot exceed 500 characters.");

        return errors;
    }

    #endregion
}