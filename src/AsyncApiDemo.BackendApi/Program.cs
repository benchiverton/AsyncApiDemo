using AsyncApiDemo.BackendApi;
using AsyncApiDemo.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<OrderCounter>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/sendorder/{orderNumber:int}", (int orderNumber, OrderCounter orderCounter) =>
    {
        app.Logger.LogInformation("Sending order {orderNumber}...", orderNumber);
        var orderId = Guid.CreateVersion7();
        Task.Delay(1000); // do something
        app.Logger.LogInformation("Order {orderNumber} sent. Unique id: {orderId}", orderNumber, orderId);

        orderCounter.Increment();
        
        return Results.Ok(orderId);
    })
    .WithName("SendOrder");

app.MapGet("/ordercount", (OrderCounter orderCounter) =>
    {
        var orderCount = orderCounter.GetCount();
        return Results.Ok(orderCount);
    })
    .WithName("OrderCount");

app.MapDefaultEndpoints();

app.Run();
