namespace AsyncApiDemo.BackendApi;

public class OrderCounter
{
    private static int _orderId;

    public void Increment()
    {
        Interlocked.Increment(ref _orderId);
    }

    public int GetCount() => _orderId;
}