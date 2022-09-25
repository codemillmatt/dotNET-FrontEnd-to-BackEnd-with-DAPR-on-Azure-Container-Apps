using Bogus;
using Dapr.Client;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDaprClient();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationMonitoring();

var app = builder.Build();

app.UseCloudEvents();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// generae the list of products
var products = new Faker<Product>()
    .StrictMode(true)
    .RuleFor(p => p.ProductId, (f, p) => f.Database.Random.Guid())
    .RuleFor(p => p.ProductName, (f, p) => f.Commerce.ProductName()).Generate(10);

// mapget for all the products
app.MapGet("/products", async (DaprClient daprClient) => {

    // grab the products from the store
    var storeProducts = await daprClient.GetStateAsync<List<Product>>("state", "products");

    if (storeProducts == null || storeProducts?.Count == 0)
        storeProducts = new List<Product>(products);

    // store the products as they are
    await daprClient.SaveStateAsync<List<Product>>("state", "products", storeProducts);

    return Results.Ok(storeProducts);
})
   .Produces<Product[]>(StatusCodes.Status200OK)
   .WithName("GetProducts");


app.MapDelete("/products/{productId}", async (string productId, DaprClient daprClient) =>
{
    var storeProducts = await daprClient.GetStateAsync<List<Product>>("state", "products");

    if (storeProducts == null || storeProducts?.Count == 0)
        return Results.NotFound("nothing in dapr store");

    var product = storeProducts?.Find(p => p.ProductId.ToString() == productId);

    if (product == null)
        return Results.NotFound("nothing in returned store");

    storeProducts?.Remove(product);

    await daprClient.SaveStateAsync("state", "products", storeProducts);

    await daprClient.PublishEventAsync("pubsub", "deleteInventory", JsonConvert.SerializeObject(product));
    
    return Results.Ok();
})
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithName("DeleteProduct");

app.Run();

public class Product
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
}