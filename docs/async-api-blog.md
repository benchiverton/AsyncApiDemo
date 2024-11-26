# [WIP] Async API

How you can enhance your API's using messaging.

## The problem

A Gateway API has been designed to accept orders. It's expected to recieve a very high throughput of orders, which it must save to another system via a Backend API.

flowchart TD
    A[Client] -->|1️⃣ Submit Order| B(Gateway API)
    B -->|2️⃣ Save Order| C(Backend API)
    C -->|3️⃣ Order Saved| B
    B -->|4️⃣ 200 Ok| A

As performance testing starts, the results look promising. It seems the API meets the throughput requirements.

GRAPH

However, as I continue to increase the load, something unexpected happens - beyond a certain point, throughput **decreases**!

GRAPH

### What's going on?

When the gateway API makes a remote call, state needs to be stored in memory so that the program can continue upon reciept of the response. If this remote call is not almost instant, then it's very likely this memory will promoted from Gen0 -> Gen1 -> Gen2 by the garbage collector. As Gen2 is not actively cleaned up by the garbage collector, this is problematic - even if a response is recieved the memory may not be cleaned up.

Eventually the garbage collector will suspend threads to clean up the Gen2 memory. As the app is not running whilst the garbage collector is running, the response time will start to increase.

As more requests arrive, this problem is compounded - response times increase causing memory to be promoted to Gen2 faster, resulting in the garbage collector suspending threads to clean up Gen2 memory more regularly, increasing response times, increasing Gen2 memory, etc. This causes the throughput to decrease as the load increases.

## One (of many) solutions

One possible solution is to use *messaging* to:
1. Decouple the Submit Order request at the Gateway API from the Save Order request at the Backend API, by enqueing a 'SendOrder' message on a message queue.
1. Have a seperate process consuming messages on the queue and calling the 'Save Order' endpoint. The key here is to make sure the process consuming messages **limits concurrent message processing**, to avoid the issues described above.
1. Return a '202 Accepted' message over a '200 Ok', indicating that the order has been recieved however processing is incomplete.

flowchart TD
    A[Client] -->|1️⃣ Submit Order| B(Gateway API)
    B -->|2️⃣ Enqueue Order | Q[Queue]
    B -->|3️⃣ 202 Accepted| A
    B -->|① Dequeue Order | Q[Queue]
    B -->|② Save Order | C(Backend API)
    C -->|③ Order Saved | B

Implementing this pattern in my example API using MassTransit yielded the following results:

### Limitations
* If the backend API does validation, you'll need to implement these as possible error states on other API endpoints. This allows API clients to understand what went wrong and how to recover.
   * Consider performing basic validation on the Gateway API (e.g. duplicate checks) to avoid too many erroneous orders entering the system.

## Appendix

### Raw test results

