using System.Windows.Input;
using MassTransit;

namespace AsyncApiDemo.GatewayApi;

public class SubmitOrderRequestConsumer : IConsumer<SubmitOrderRequest>
{
    private readonly ILogger<SubmitOrderRequestConsumer> _logger;
    private readonly HttpClient _httpClient;

    public SubmitOrderRequestConsumer(ILogger<SubmitOrderRequestConsumer> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task Consume(ConsumeContext<SubmitOrderRequest> context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost:7354/sendorder/" + context.Message.OrderNumber);
        using var response = await _httpClient.SendAsync(request, context.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public record SubmitOrderRequest(int OrderNumber);
