namespace Orderflow.Orders.Clients;

public class CatalogClient(IHttpClientFactory httpClientFactory, ILogger<CatalogClient> logger) : ICatalogClient
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("catalog");

    public async Task<ProductInfo?> GetProductAsync(int productId)
    {
        try
        {
            var response = await _http.GetAsync($"/api/v1/products/{productId}");

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Product {ProductId} not found. Status: {Status}", productId, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ProductInfo>();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to get product {ProductId} from Catalog", productId);
            return null;
        }
    }

    public async Task<bool> ReserveStockAsync(int productId, int quantity)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"/api/v1/products/{productId}/stock/reserve",
                new { Quantity = quantity });  // ← CAMBIO: Quantity con mayúscula

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogWarning("Failed to reserve stock for product {ProductId}. Status: {Status}, Error: {Error}",
                    productId, response.StatusCode, error);
            }

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to reserve stock for product {ProductId}", productId);
            return false;
        }
    }

    public async Task<bool> ReleaseStockAsync(int productId, int quantity)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"/api/v1/products/{productId}/stock/release",
                new { Quantity = quantity });  // ← CAMBIO: Quantity con mayúscula

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogWarning("Failed to release stock for product {ProductId}. Status: {Status}, Error: {Error}",
                    productId, response.StatusCode, error);
            }

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to release stock for product {ProductId}", productId);
            return false;
        }
    }
}