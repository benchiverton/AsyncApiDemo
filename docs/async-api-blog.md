# Asynchronous messaging in synchronous APIs

## The problem

A Gateway API has been designed to accept orders. It's expected to receive a very high throughput of orders, which it must save to another system via a Backend API.

```mermaid
flowchart TD
    A((Client)) -->|1️⃣ Submit Order| B(Gateway API)
    B -->|2️⃣ Save Order| C(Backend API)
    C -->|3️⃣ Order Saved| B
    B -->|4️⃣ 200 Ok| A
```

As performance testing starts, the results look promising. It seems the API meets the throughput requirements.

![](images/graph-sync-beginning.png)

However, as the load continues to increase, something unexpected happens - beyond a certain point, throughput **decreases**!

![](images/graph-sync-all.png)

Upon further debugging, the IDE highlighted that the piece of code making the synchronous 'SendOrder' request to the backend API is allocating a significant amount of memory on the small object heap.

![](images/soh-allocation.png)

### What's going on?

When the Gateway API makes a remote call, state needs to be stored in memory to resume processing upon receipt of the response. If the call takes a long time, the associated objects may survive multiple garbage collection (GC) cycles, potentially being promoted from Gen0 (short-lived objects) to Gen1 and eventually to Gen2 (long-lived objects). Since Gen2 collections are less frequent and more expensive, memory pressure in Gen2 can degrade performance.

Under high load, frequent promotions to Gen2 can trigger full GC cycles more often. During these cycles, the garbage collector suspends application threads to clean up memory, causing response times to increase. This feedback loop—longer response times causing more memory promotions—leads to increased GC activity and degraded throughput as the load increases.

## One (of many) solutions

One possible solution is to use **asynchronous messaging** to:
1. Decouple the "Submit Order" request at the Gateway API from the "Save Order" request at the Backend API, instead enqueuing a `SendOrder` message on a message queue.
   * **Tip:** Return a `202 Accepted` HTTP status instead of a `200 Ok`, indicating that the order has been received, but processing is incomplete.
1. Have a seperate process consume messages from the queue and save the order.
   * **Tip:** Ensure the consumer *limits concurrent message processing* to avoid the same issue faced by the synchronous implementation.

```mermaid
flowchart TD
    A((Client)) -->|1️⃣ Submit Order| B(Gateway API)
    B -->|2️⃣ Enqueue Order | Q>Message queue]
    B -->|3️⃣ 202 Accepted| A
    B -->|① Dequeue Order | Q
    B -->|② Save Order | C(Backend API)
    C -->|③ Order Saved | B
```

### Example

* [Async endpoint](https://github.com/benchiverton/AsyncApiDemo/blob/main/src/AsyncApiDemo.GatewayApi/Program.cs#L67)
* [Message consumer](https://github.com/benchiverton/AsyncApiDemo/tree/main/src/AsyncApiDemo.GatewayApi/SubmitOrderRequestConsumer.cs)

With this implementation, throughput does not degrade as load increases. Instead, throughput rises with load until it reaches a maximum, beyond which it remains constant. This maximum represents the highest throughput your message consumer can support and can be increased by running more consumers in parallel.

![](images/graph-all.png)

### Limitations
* If the Backend API performs pre-processing (such as validation), you’ll need to implement error states within your domain model so that clients can query failed orders.
   * Consider performing basic validation at the Gateway API (e.g., duplicate checks) to prevent too many erroneous orders from entering the system.

## Appendix

Testing parameters:
* Order sender MaxDegreeOfParallelism = 1000 (i.e. at most 1000 concurrent SubmitOrder requests)
* Backend API processes requests for 50ms (mocked using Task.Delay(50))
* All apps run on one machine - this could be improved in the future

### Raw test results


| Endpoint             | Requests   | Average latency (ms) | Throughput (/min)    | Average failures     |
| -- | -- | -- | -- | -- |
| submitordersync      |         10 |                  131 |                7,974 |                    0 |
| submitorderasync     |         10 |                   24 |                8,437 |                    0 |
| submitordersync      |         50 |                  355 |               11,998 |                    0 |
| submitorderasync     |         50 |                    3 |               14,172 |                    0 |
| submitordersync      |        100 |                  815 |                7,046 |                    0 |
| submitorderasync     |        100 |                    3 |               15,773 |                    0 |
| submitordersync      |        200 |                3,244 |                3,733 |                    0 |
| submitorderasync     |        200 |                    7 |               16,976 |                    0 |
| submitordersync      |        500 |                8,073 |                3,342 |                    0 |
| submitorderasync     |        500 |                   16 |               17,985 |                    0 |
| submitordersync      |       1000 |               18,833 |                3,083 |                  197 |
| submitorderasync     |       1000 |                   25 |               17,535 |                    0 |
| submitordersync      |       2000 |               22,289 |                2,834 |                 3903 |
| submitorderasync     |       2000 |                1,068 |               18,118 |                    0 |
| submitordersync      |       3000 |               20,745 |                3,264 |                 1568 |
| submitorderasync     |       3000 |                  100 |               17,047 |                    0 |