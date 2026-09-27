using Data;
using Microsoft.EntityFrameworkCore;
using Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SupabaseDb")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors("AllowAll");
app.UseHttpsRedirection();

// Health Check
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "InventoryApi",
    timestamp = DateTimeOffset.UtcNow
}));

// GET /products
app.MapGet("/products", async (InventoryDbContext db) =>
{
    var products = await db.Products
        .AsNoTracking()
        .Include(p => p.StockChanges)
        .OrderBy(p => p.ProductId)
        .Select(p => new ProductResponse(
            p.ProductId,
            p.Name,
            p.Sku,
            p.Stock,
            p.StockChanges
                .OrderByDescending(s => s.Date)
                .Select(s => new StockChangeResponse(s.StockChangesId, s.ProductId, s.Date, s.Delta, s.Source))
                .ToList()
        ))
        .ToListAsync();

    return Results.Ok(products);
});

// GET /products/{id}
app.MapGet("/products/{id:int}", async (int id, InventoryDbContext db) =>
{
    var product = await db.Products
        .AsNoTracking()
        .Include(p => p.StockChanges)
        .FirstOrDefaultAsync(p => p.ProductId == id);

    if (product == null)
    {
        return Results.NotFound(new ErrorResponse("Product not found."));
    }

    var response = new ProductResponse(
        product.ProductId,
        product.Name,
        product.Sku,
        product.Stock,
        product.StockChanges
            .OrderByDescending(s => s.Date)
            .Select(s => new StockChangeResponse(s.StockChangesId, s.ProductId, s.Date, s.Delta, s.Source))
            .ToList()
    );

    return Results.Ok(response);
});

// POST /products
app.MapPost("/products", async (CreateProductRequest request, InventoryDbContext db) =>
{
    var name = request.Name?.Trim();
    var sku = request.Sku?.Trim().ToUpperInvariant();

    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest(new ErrorResponse("Product name cannot be empty."));
    }

    if (string.IsNullOrWhiteSpace(sku))
    {
        return Results.BadRequest(new ErrorResponse("SKU cannot be empty."));
    }

    if (request.Stock < 0)
    {
        return Results.BadRequest(new ErrorResponse("Initial stock cannot be negative."));
    }

    var skuExists = await db.Products.AnyAsync(p => p.Sku == sku);
    if (skuExists)
    {
        return Results.Conflict(new ErrorResponse("A product with this SKU already exists."));
    }

    var product = new Product
    {
        Name = name,
        Sku = sku,
        Stock = request.Stock
    };

    if (request.Stock > 0)
    {
        product.StockChanges.Add(new StockChanges
        {
            Date = DateTimeOffset.UtcNow,
            Delta = request.Stock,
            Source = "Initial Stock"
        });
    }

    db.Products.Add(product);
    await db.SaveChangesAsync();

    var response = new ProductResponse(
        product.ProductId,
        product.Name,
        product.Sku,
        product.Stock,
        product.StockChanges
            .OrderByDescending(s => s.Date)
            .Select(s => new StockChangeResponse(s.StockChangesId, s.ProductId, s.Date, s.Delta, s.Source))
            .ToList()
    );

    return Results.Created($"/products/{product.ProductId}", response);
});

// PUT /products/{id}
app.MapPut("/products/{id:int}", async (int id, UpdateProductRequest request, InventoryDbContext db) =>
{
    var name = request.Name?.Trim();
    var sku = request.Sku?.Trim().ToUpperInvariant();

    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest(new ErrorResponse("Product name cannot be empty."));
    }

    if (string.IsNullOrWhiteSpace(sku))
    {
        return Results.BadRequest(new ErrorResponse("SKU cannot be empty."));
    }

    if (request.Stock < 0)
    {
        return Results.BadRequest(new ErrorResponse("Stock cannot be negative."));
    }

    var existingProduct = await db.Products
        .Include(p => p.StockChanges)
        .FirstOrDefaultAsync(p => p.ProductId == id);

    if (existingProduct == null)
    {
        return Results.NotFound(new ErrorResponse("Product not found."));
    }

    var skuConflict = await db.Products.AnyAsync(p => p.Sku == sku && p.ProductId != id);
    if (skuConflict)
    {
        return Results.Conflict(new ErrorResponse("A product with this SKU already exists."));
    }

    var stockDelta = request.Stock - existingProduct.Stock;
    if (stockDelta != 0)
    {
        existingProduct.StockChanges.Add(new StockChanges
        {
            ProductId = id,
            Date = DateTimeOffset.UtcNow,
            Delta = stockDelta,
            Source = "Manual Edit"
        });
    }

    existingProduct.Name = name;
    existingProduct.Sku = sku;
    existingProduct.Stock = request.Stock;

    await db.SaveChangesAsync();

    var response = new ProductResponse(
        existingProduct.ProductId,
        existingProduct.Name,
        existingProduct.Sku,
        existingProduct.Stock,
        existingProduct.StockChanges
            .OrderByDescending(s => s.Date)
            .Select(s => new StockChangeResponse(s.StockChangesId, s.ProductId, s.Date, s.Delta, s.Source))
            .ToList()
    );

    return Results.Ok(response);
});

// POST /products/{id}/adjust-stock
app.MapPost("/products/{id:int}/adjust-stock", async (int id, AdjustStockRequest request, InventoryDbContext db) =>
{
    var existingProduct = await db.Products
        .Include(p => p.StockChanges)
        .FirstOrDefaultAsync(p => p.ProductId == id);

    if (existingProduct == null)
    {
        return Results.NotFound(new ErrorResponse("Product not found."));
    }

    if (existingProduct.Stock + request.Delta < 0)
    {
        return Results.BadRequest(new ErrorResponse("Insufficient stock for this deduction. Stock cannot be negative."));
    }

    existingProduct.Stock += request.Delta;
    var stockChange = new StockChanges
    {
        ProductId = id,
        Date = DateTimeOffset.UtcNow,
        Delta = request.Delta,
        Source = string.IsNullOrWhiteSpace(request.Source) ? "Stock Adjustment" : request.Source.Trim()
    };

    existingProduct.StockChanges.Add(stockChange);
    await db.SaveChangesAsync();

    var response = new ProductResponse(
        existingProduct.ProductId,
        existingProduct.Name,
        existingProduct.Sku,
        existingProduct.Stock,
        existingProduct.StockChanges
            .OrderByDescending(s => s.Date)
            .Select(s => new StockChangeResponse(s.StockChangesId, s.ProductId, s.Date, s.Delta, s.Source))
            .ToList()
    );

    return Results.Ok(response);
});

// DELETE /products/{id}
app.MapDelete("/products/{id:int}", async (int id, InventoryDbContext db) =>
{
    var existingProduct = await db.Products.FindAsync(id);
    if (existingProduct == null)
    {
        return Results.NotFound(new ErrorResponse("Product not found."));
    }

    db.Products.Remove(existingProduct);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

// Make Program accessible to integration tests
public partial class Program { }