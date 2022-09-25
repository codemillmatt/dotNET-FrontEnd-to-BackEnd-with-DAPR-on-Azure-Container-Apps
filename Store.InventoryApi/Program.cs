using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache(); // we'll use cache to simulate storage
builder.Services.AddDaprClient();
builder.Services.AddApplicationMonitoring();

var app = builder.Build();

app.UseCloudEvents();
app.MapSubscribeHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/inventory/{productId}", (string productId, IMemoryCache memoryCache) =>
{
    var memCacheKey = $"{productId}-inventory";
    int inventoryValue = -404;

    if (!memoryCache.TryGetValue(memCacheKey, out inventoryValue))
    {
        inventoryValue = new Random().Next(1, 100);
        memoryCache.Set(memCacheKey, inventoryValue);
    }

    inventoryValue = memoryCache.Get<int>(memCacheKey);

    return Results.Ok(inventoryValue);
})
.Produces<int>(StatusCodes.Status200OK)
.WithName("GetInventoryCount");

app.MapPost("/inventory/delete", (IMemoryCache memoryCache, Product data) =>
{
    var memCacheKey = $"{data.ProductId}-inventory";
    int inventoryValue = -404;

    if (!memoryCache.TryGetValue(memCacheKey, out inventoryValue))
    {
        return Results.NotFound();
    }

    // remove all the inventory
    inventoryValue = 0;

    memoryCache.Set(memCacheKey, inventoryValue);

    return Results.Ok(inventoryValue);
})
.Produces<int>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithTopic("pubsub", "deleteInventory")
.WithName("DeleteInventory");

app.Run();

public class Product
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
}