using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Models;
using Xunit;

namespace InventoryApi.Tests;

public class ProductEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_CreatesProduct_AndAddsInitialStockHistory()
    {
        var request = new CreateProductRequest("MacBook Pro", "MBP-M3", 10);
        var response = await _client.PostAsJsonAsync("/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        Assert.Equal("MacBook Pro", product.Name);
        Assert.Equal("MBP-M3", product.Sku);
        Assert.Equal(10, product.Stock);
        Assert.Single(product.StockChanges);
        Assert.Equal(10, product.StockChanges[0].Delta);
        Assert.Equal("Initial Stock", product.StockChanges[0].Source);
    }

    [Fact]
    public async Task PostProduct_Returns400_WhenFieldsAreInvalid()
    {
        var emptyName = new CreateProductRequest("", "VALID-SKU", 5);
        var res1 = await _client.PostAsJsonAsync("/products", emptyName);
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);

        var emptySku = new CreateProductRequest("Valid Name", "", 5);
        var res2 = await _client.PostAsJsonAsync("/products", emptySku);
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);

        var negativeStock = new CreateProductRequest("Valid Name", "SKU-NEG", -1);
        var res3 = await _client.PostAsJsonAsync("/products", negativeStock);
        Assert.Equal(HttpStatusCode.BadRequest, res3.StatusCode);
    }

    [Fact]
    public async Task PostProduct_Returns409Conflict_WhenSkuAlreadyExists()
    {
        var req1 = new CreateProductRequest("iPad Air", "IPAD-AIR-1", 5);
        var res1 = await _client.PostAsJsonAsync("/products", req1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        var req2 = new CreateProductRequest("iPad Air Clone", "IPAD-AIR-1", 3);
        var res2 = await _client.PostAsJsonAsync("/products", req2);
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);

        var error = await res2.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("A product with this SKU already exists.", error.Error);
    }

    [Fact]
    public async Task PutProduct_UpdatesProduct_AndTracksDeltaStock()
    {
        var createReq = new CreateProductRequest("Keychron K2", "KCR-K2", 15);
        var createRes = await _client.PostAsJsonAsync("/products", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);

        var updateReq = new UpdateProductRequest("Keychron K2 Pro", "KCR-K2-PRO", 20);
        var updateRes = await _client.PutAsJsonAsync($"/products/{created.ProductId}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Keychron K2 Pro", updated.Name);
        Assert.Equal("KCR-K2-PRO", updated.Sku);
        Assert.Equal(20, updated.Stock);
        // Initial Stock (15) + Manual Edit (+5)
        Assert.Equal(2, updated.StockChanges.Count);
        Assert.Equal(5, updated.StockChanges[0].Delta);
    }

    [Fact]
    public async Task AdjustStock_AppliesDelta_AndRecordsHistory()
    {
        var createReq = new CreateProductRequest("AirPods Pro", "APP-2", 20);
        var createRes = await _client.PostAsJsonAsync("/products", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);

        // Deduct 5 units
        var adjustReq = new AdjustStockRequest(-5, "Customer Purchase");
        var adjustRes = await _client.PostAsJsonAsync($"/products/{created.ProductId}/adjust-stock", adjustReq);
        Assert.Equal(HttpStatusCode.OK, adjustRes.StatusCode);

        var adjusted = await adjustRes.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(adjusted);
        Assert.Equal(15, adjusted.Stock);
        Assert.Equal(-5, adjusted.StockChanges[0].Delta);
        Assert.Equal("Customer Purchase", adjusted.StockChanges[0].Source);

        // Attempting to deduct more than stock returns 400
        var overDeductReq = new AdjustStockRequest(-30, "Large Deduction");
        var overDeductRes = await _client.PostAsJsonAsync($"/products/{created.ProductId}/adjust-stock", overDeductReq);
        Assert.Equal(HttpStatusCode.BadRequest, overDeductRes.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_DeletesProduct_AndReturns204()
    {
        var createReq = new CreateProductRequest("Disposable Item", "DISP-01", 1);
        var createRes = await _client.PostAsJsonAsync("/products", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);

        var delRes = await _client.DeleteAsync($"/products/{created.ProductId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // Getting it should now return 404
        var getRes = await _client.GetAsync($"/products/{created.ProductId}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }
}
