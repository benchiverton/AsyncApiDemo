var builder = DistributedApplication.CreateBuilder(args);

var backendApi = builder.AddProject<Projects.AsyncApiDemo_BackendApi>("backendapi")
    .WithHttpsHealthCheck("/health");
var gatewayApi = builder.AddProject<Projects.AsyncApiDemo_GatewayApi>("gatewayapi")
    .WithHttpsHealthCheck("/health");

var worker = builder.AddProject<Projects.AsyncApiDemo_OrderSender>("ordersender")
    .WaitFor(backendApi)
    .WaitFor(gatewayApi);

builder.Build().Run();
