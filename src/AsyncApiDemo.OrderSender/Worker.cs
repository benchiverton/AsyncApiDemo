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
        List<Result> results =
        [
            await RunTests(SyncEndpoint, 10, 3),
            await RunTests(AsyncEndpoint, 10, 3),
            await RunTests(SyncEndpoint, 50, 3),
            await RunTests(AsyncEndpoint, 50, 3),
            await RunTests(SyncEndpoint, 100, 3),
            await RunTests(AsyncEndpoint, 100, 3),
            await RunTests(SyncEndpoint, 200, 3),
            await RunTests(AsyncEndpoint, 200, 3),
            await RunTests(SyncEndpoint, 500, 3),
            await RunTests(AsyncEndpoint, 500, 3),
            await RunTests(SyncEndpoint, 1000, 3),
            await RunTests(AsyncEndpoint, 1000, 3),
            await RunTests(SyncEndpoint, 2000, 3),
            await RunTests(AsyncEndpoint, 2000, 3),
            await RunTests(SyncEndpoint, 5000, 3),
            await RunTests(AsyncEndpoint, 5000, 3)
        ];

        var resultsLog = new StringBuilder();
        resultsLog.AppendLine(
            $"| {"Endpoint",-20} | {"Requests",-10} | {"Average latency (ms)",-20} | {"Throughput (/min)",-20} | {"Average failures",-20} |");
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
            results.Select(r => r.LatencyMs).Average(),
            (long)results.Select(r => r.ThroughputPerMin).Average(),
            (int)results.Select(r => r.NumberOfFailures).Average());
    }

    private async Task<Result> RunTest(string endpoint, int numberOfRequests)
    {
        _logger.LogInformation("Starting test. Endpoint: {endpoint}, #Requests: {numberOfRequests}", endpoint,
            numberOfRequests);

        var initialOrderCount = await GetOrderCount();

        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, numberOfRequests).Select(i => SubmitOrder(endpoint, initialOrderCount + i))
            .ToList();
        await Task.WhenAll(tasks);

        // wait for all orders to be processed
        var result = await GetOrderCount();
        while (result - initialOrderCount < numberOfRequests)
        {
            await Task.Delay(50);
            result = await GetOrderCount();
        }

        var allOrdersProcessedTime = sw.ElapsedMilliseconds;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        return new Result(
            endpoint,
            numberOfRequests,
            tasks.Select(t => t.Result.duration).Average(),
            (long)(numberOfRequests / ((double)allOrdersProcessedTime / 60_000)),
            tasks.Select(t => t.Result.failures).Sum());
    }

    private async Task<(int failures, long duration)> SubmitOrder(string endpoint, int requestId)
    {
        var sw = Stopwatch.StartNew();
        var failures = 0;
        var success = false;
        while (!success)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://localhost:7355/{endpoint}/{requestId}");
                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                success = true;
            }
            catch (Exception ex)
            {
                failures++;
            }
        }

        return (failures, sw.ElapsedMilliseconds);
    }

    private async Task<int> GetOrderCount()
    {
        using var getOrderCountRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost:7354/ordercount");
        using var response = await _httpClient.SendAsync(getOrderCountRequest);
        return int.Parse(await response.Content.ReadAsStringAsync());
    }

    private record Result(
        string Endpoint,
        int NumberOfRequests,
        double LatencyMs,
        long ThroughputPerMin,
        int NumberOfFailures)
    {
        public override string ToString()
        {
            return
                $"| {Endpoint,-20} | {NumberOfRequests,10} | {LatencyMs,20:N0} | {ThroughputPerMin,20:N0} | {NumberOfFailures,20} |";
        }
    }
}