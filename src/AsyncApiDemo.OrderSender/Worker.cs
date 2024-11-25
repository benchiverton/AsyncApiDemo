using System.Diagnostics;

namespace AsyncApiDemo.OrderSender;

public class Worker : BackgroundService
{
    private const int NumberOfRequests = 100;
    private const string SyncEndpoint = "submitordersync";
    private const string AsyncEndpoint = "submitorderasync";

    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _httpClient;

    public Worker(ILogger<Worker> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Running test for endpoint: {SyncEndpoint}...", SyncEndpoint);
        var syncResults = await RunTest(SyncEndpoint, NumberOfRequests);
        _logger.LogInformation("Running test for endpoint: {AsyncEndpoint}...", AsyncEndpoint);
        var asyncResults = await RunTest(AsyncEndpoint, NumberOfRequests);
            
        _logger.LogInformation(
            "Results for endpoint: {SyncEndpoint}. {numberOfRequests} sent in {sendTime}ms, processed in {processedTime}ms",
            SyncEndpoint, 
            syncResults.NumberOfRequests, 
            syncResults.AllOrdersSentMs,
            syncResults.AllOrdersProcessedMs);
        _logger.LogInformation(
            "Results for endpoint: {AsyncEndpoint}. {numberOfRequests} sent in {sendTime}ms, processed in {processedTime}ms",
            AsyncEndpoint, 
            asyncResults.NumberOfRequests, 
            asyncResults.AllOrdersSentMs,
            asyncResults.AllOrdersProcessedMs);
    }

    private async Task<Results> RunTest(string endpoint, int numberOfRequests)
    {
        var initialOrderCount = await GetOrderCount();

        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, numberOfRequests).Select(i =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://localhost:7355/{endpoint}/{i}");
            return _httpClient.SendAsync(request);
        });
        await Task.WhenAll(tasks);
        var allOrdersSentTime = sw.ElapsedMilliseconds;

        var currentOrderCount = await GetOrderCount();
        while (currentOrderCount - initialOrderCount < numberOfRequests)
        {
            await Task.Delay(100);
            currentOrderCount = await GetOrderCount();
        }

        var allOrdersProcessedTime = sw.ElapsedMilliseconds;

        return new Results(numberOfRequests, allOrdersSentTime, allOrdersProcessedTime);
    }

    private async Task<int> GetOrderCount()
    {
        var getOrderCountRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost:7354/ordercount");
        var response = await _httpClient.SendAsync(getOrderCountRequest);
        return int.Parse(await response.Content.ReadAsStringAsync());
    }

    private record Results(int NumberOfRequests, double AllOrdersSentMs, double AllOrdersProcessedMs);
}