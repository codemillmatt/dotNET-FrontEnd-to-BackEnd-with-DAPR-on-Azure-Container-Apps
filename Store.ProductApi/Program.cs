using Bogus;
using Dapr.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDaprClient();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationMonitoring();

var app = builder.Build();

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
app.MapGet("/products", () => Results.Ok(products))
   .Produces<Product[]>(StatusCodes.Status200OK)
   .WithName("GetProducts");


app.MapDelete("/products/{productName}", async (string productName, DaprClient daprClient) =>
{
    var product = products.Find(p => p.ProductName.Equals(productName, StringComparison.OrdinalIgnoreCase));

    if (product == null)
        return Results.NotFound();

    products.Remove(product);

    await daprClient.PublishEventAsync("pubsub", "deleteInventory", productName);

    return Results.Ok();
})
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithName("DeleteProduct");

app.Run();

public class Product
{
    public Guid ProductId => Guid.NewGuid();
    public string ProductName { get; set; }
}