using Data;
using Microsoft.EntityFrameworkCore;
using Models;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SupabaseDb")));

builder.Services.AddOpenApi();

var app = builder.Build();


app.UseHttpsRedirection();
app.MapGet("/products", async (InventoryDbContext db) =>
    await db.Products.ToListAsync());


app.MapPost("/products", async (Product product, InventoryDbContext db) =>
    {
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return Results.Created($"/products/{product.ProductId}", product);
    });

app.MapPut("/products/{id}", async (int id, Product product, InventoryDbContext db) =>
    {
        var existingProduct = await db.Products.FindAsync(id);
        if (existingProduct == null)
        {
            return Results.NotFound();
        }

        existingProduct.Name = product.Name;
        existingProduct.Sku = product.Sku;
        existingProduct.Stock = product.Stock;

        await db.SaveChangesAsync();
        return Results.Ok(existingProduct);
    });

app.MapDelete("/products/{id}", async (int id, InventoryDbContext db) =>
    {
        var existingProduct = await db.Products.FindAsync(id);
        if (existingProduct == null)
        {
            return Results.NotFound();
        }

        db.Products.Remove(existingProduct);
        await db.SaveChangesAsync();
        return Results.NoContent();
    });

app.Run();