using Dapr;
using Dapr.Client;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

app.MapGet("/inventory/{productId}", async (string productId, DaprClient client) =>
{
    var memCacheKey = $"{productId}-inventory";
    int inventoryValue = -404;

    inventoryValue = await client.GetStateAsync<int>("state", memCacheKey);

    if (inventoryValue == 0)
    {
        inventoryValue = new Random().Next(1, 100);
        await client.SaveStateAsync("state", memCacheKey, inventoryValue);
    }

    return Results.Ok(inventoryValue);
})
.Produces<int>(StatusCodes.Status200OK)
.WithName("GetInventoryCount");

app.MapPost("/inventory/delete", async (DaprClient client, [FromBody]Product data) =>
{
    var memCacheKey = $"{data.ProductId}-inventory";
    int inventoryValue = -404;

    inventoryValue = await client.GetStateAsync<int>("state", memCacheKey);

    if (inventoryValue == 0)
        return Results.NotFound($"nothing in the cache for {memCacheKey}");

    inventoryValue = -404;

    await client.SaveStateAsync("state", memCacheKey, inventoryValue);
    
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