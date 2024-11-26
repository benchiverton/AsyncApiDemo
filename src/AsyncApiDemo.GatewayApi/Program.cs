using AsyncApiDemo.GatewayApi;
using AsyncApiDemo.ServiceDefaults;
using Microsoft.Extensions.Caching.Memory;


var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// Configure messaging
var endpointConfiguration = new EndpointConfiguration("AsyncApiDemo");
endpointConfiguration.UseSerialization<SystemJsonSerializer>();
endpointConfiguration.EnableInstallers();
endpointConfiguration.UseTransport<LearningTransport>();
builder.UseNServiceBus(endpointConfiguration);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.MapPost("/submitordersync/{orderNumber:int}", async (int orderNumber, IMemoryCache cache, HttpClient httpClient) =>
    {
        // validate
        var key = $"SYNC_{orderNumber}";
        var exists = cache.Get<string>(key);
        if (!string.IsNullOrEmpty(exists))
        {
            // duplicate
            return Results.BadRequest("Order number already exists.");
        }

        // send
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost:7354/sendorder/" + orderNumber);
        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        cache.Set(key, orderNumber.ToString());
        app.Logger.LogInformation("Order {orderNumber} submitted.", orderNumber);

        return Results.Ok();
    })
    .WithName("SubmitOrderSync");

app.MapPost("/submitorderasync/{orderNumber:int}", async (int orderNumber, IMemoryCache cache, IMessageSession messageSession) =>
    {
        // validate
        var key = $"ASYNC_{orderNumber}";
        var exists = cache.Get<string>(key);
        if (!string.IsNullOrEmpty(exists))
        {
            // duplicate
            return Results.BadRequest("Order number already exists.");
        }

        // enqueue
        await messageSession.SendLocal(new SubmitOrderRequest(orderNumber));
        cache.Set(key, orderNumber.ToString());
        app.Logger.LogInformation("Order {orderNumber} enqueued.", orderNumber);

        return Results.Accepted();
    })
    .WithName("SubmitOrderAsync");

app.MapDefaultEndpoints();

app.Run();
