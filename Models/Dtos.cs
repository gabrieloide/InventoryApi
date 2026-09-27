using System;
using System.Collections.Generic;

namespace Models
{
    public record CreateProductRequest(
        string Name,
        string Sku,
        int Stock
    );

    public record UpdateProductRequest(
        string Name,
        string Sku,
        int Stock
    );

    public record AdjustStockRequest(
        int Delta,
        string? Source
    );

    public record StockChangeResponse(
        int StockChangesId,
        int ProductId,
        DateTimeOffset Date,
        int Delta,
        string Source
    );

    public record ProductResponse(
        int ProductId,
        string Name,
        string Sku,
        int Stock,
        List<StockChangeResponse> StockChanges
    );

    public record ErrorResponse(
        string Error,
        string? Details = null
    );
}
