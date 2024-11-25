namespace AsyncApiDemo.GatewayApi;

public class SubmitOrderRequestHandler : IHandleMessages<SubmitOrderRequest>
{
    private readonly ILogger<SubmitOrderRequestHandler> _logger;
    private readonly HttpClient _httpClient;

    public SubmitOrderRequestHandler(ILogger<SubmitOrderRequestHandler> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task Handle(SubmitOrderRequest message, IMessageHandlerContext context)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost:7354/sendorder/" + message.OrderNumber);
        var response = await _httpClient.SendAsync(request, context.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public record SubmitOrderRequest(int OrderNumber) : ICommand;
