using System.Diagnostics;
using System.Text;

namespace AsyncApiDemo.OrderSender;

public class Worker : BackgroundService
{
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
        _logger.LogInformation("Running tests...");
        var syncResults_10 = await RunTests(SyncEndpoint, 10, 3);
        var asyncResults_10 = await RunTests(AsyncEndpoint, 10, 3);
        var syncResults_50 = await RunTests(SyncEndpoint, 50, 3);
        var asyncResults_50 = await RunTests(AsyncEndpoint, 50, 3);
        var syncResults_100 = await RunTests(SyncEndpoint, 100, 3);
        var asyncResults_100 = await RunTests(AsyncEndpoint, 100, 3);
        var syncResults_200 = await RunTests(SyncEndpoint, 200, 3);
        var asyncResults_200 = await RunTests(AsyncEndpoint, 200, 3);
        var syncResults_500 = await RunTests(SyncEndpoint, 500, 3);
        var asyncResults_500 = await RunTests(AsyncEndpoint, 500, 3);
        var syncResults_1000 = await RunTests(SyncEndpoint, 1000, 3);
        var asyncResults_1000 = await RunTests(AsyncEndpoint, 1000, 3);

        List<Result> results =
        [
            syncResults_10,
            asyncResults_10,
            syncResults_50,
            asyncResults_50,
            syncResults_100,
            asyncResults_100,
            syncResults_200,
            asyncResults_200,
            syncResults_500,
            asyncResults_500,
            syncResults_1000,
            asyncResults_1000
        ];
        var resultsLog = new StringBuilder();
        resultsLog.AppendLine("| Endpoint             | Reqs       | Sent (ms)            | Processed (ms)       |");
        foreach (var result in results)
        {
            resultsLog.AppendLine(result.ToString());
        }

        _logger.LogInformation(resultsLog.ToString());
    }

    private async Task<Result> RunTests(string endpoint, int numberOfRequests, int numTests)
    {
        var results = new List<Result>();
        for (var i = 0; i < numTests; i++)
        {
            results.Add(await RunTest(endpoint, numberOfRequests));
        }

        return new Result(
            endpoint,
            numberOfRequests,
            results.Select(r => r.AllOrdersSentMs).Average(),
            results.Select(r => r.AllOrdersProcessedMs).Average());
    }

    private async Task<Result> RunTest(string endpoint, int numberOfRequests)
    {
        var initialOrderCount = await GetOrderCount();

        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, numberOfRequests).Select(i => SubmitOrder(endpoint, initialOrderCount + i));
        await Task.WhenAll(tasks);
        var allOrdersSentTime = sw.ElapsedMilliseconds;

        while (await GetOrderCount() - initialOrderCount < numberOfRequests)
        {
            // wait
        }

        var allOrdersProcessedTime = sw.ElapsedMilliseconds;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        return new Result(endpoint, numberOfRequests, allOrdersSentTime, allOrdersProcessedTime);
    }

    private async Task SubmitOrder(string endpoint, int requestId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://localhost:7355/{endpoint}/{requestId}");
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> GetOrderCount()
    {
        using var getOrderCountRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost:7354/ordercount");
        using var response = await _httpClient.SendAsync(getOrderCountRequest);
        return int.Parse(await response.Content.ReadAsStringAsync());
    }

    private record Result(string Endpoint, int NumberOfRequests, double AllOrdersSentMs, double AllOrdersProcessedMs)
    {
        public override string ToString()
        {
            var ordersSentMs = AllOrdersSentMs.ToString("N0");
            var ordersProcessedMs = AllOrdersProcessedMs.ToString("N0");
            return $"| {Endpoint,-20} | {NumberOfRequests,10} | {ordersSentMs,20} | {ordersProcessedMs,20} |";
        }
    }
}