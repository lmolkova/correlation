# Motivation
One of the common problems in microservices development is ability to trace request flow from client (application, browser) through all the services involved in processing.

Typical scenarios include:

1. Tracing error received by user
2. Performance analysis and optimization: whole stack of request needs to be analyzed to find where performance issues come from
3. A/B testing: metrics for requests with experimental features should be distinguished and compared to 'production' data.

These scenarios require every request to carry additional context and services to enrich their telemetry events with this context, so it would possible to correlate telemetry from all services involved in operation processing.

This standard describes context and it's format in HTTP communication.

# HTTP Protocol proposal
| Header name           |  Format    | Description |
| ----------------------| ---------- | ---------- |
| Request-Id            | Required. String | Unique identifier for every HTTP request involved in operation processing |
| Correlation-Context   | Optional. Comma separated list of key-value pairs: key1=value1, key2=value2 | Operation context which is propagated across all services involved in operation processing |
| Request-Context       | Optional **request** header. Comma separated list of key-value pairs: key3=value3, key4=value4 | Context which is passed from caller in **request** to callee  and **not** propagated further | 
| Response-Context      | Optional **response** header. Comma separated list of key-value pairs: key5=value5, key6=value6 | Context which is passed from callee in **response** to caller request and **not** propagated further | 

## Request-Id
`Request-Id` uniquely identifies every HTTP request involved in operation processing. 

Request-Id is generated on the caller side and passed to callee. Implementation should expect to receive `Request-Id` in header or MUST generate one if it was not provided (see [Root Parent Id Generation](#root-parent-id-generation) for generation considerations).
When outgoing request is made, implementation MUST generate unique `Request-Id` header and pass it to downstream service (supporting this protocol). 

Implementations SHOULD use hierarchical structure for the Id:
If Request-Id is provided from upstream service, implementation SHOULD append small id preceded with separator and pass it to downstream service, making sure every outgoing request has different suffix.
Thus, Request-Id has path structure and the root node serve as single correlation id, common for all requests involved in operation processing and implementations are ENCOURAGED to follow this approach. 

If implementation chooses not to follow this recommendation, it MUST ensure
1. It provides additional property in `Correlation-Context` serving as single unique identifier of the whole operation
2. `Request-Id` is unique for every outgoing request made in scope of the same operation

It is essential that 'incoming' and 'outgoing' Request-Ids are included in the telemetry events, so implementation of this protocol should ensure that it's possible to access the context and request-ids in particular.

## Format
`Request-Id` is a string up to 256 bytes in length.

### Formatting hierarchical `Request-Id`
`Request-Id` has following schema:

parentId.localId

ParentId is Request-Id passed from upstream service (or generated if was not provided), it may have hierarchical structure itself.
LocalId is generated to identify internal operation. It may have hierarchical structure considering service or protocol implementation may split operation to multiple activities.
- It MUST be unique for every outgoing HTTP request sent while processing the incoming request. 
- It SHOULD be small to avoid `Request-Id` overflow
- If appending localId to `Request-Id` would cause it to exceed length limit, implementation MUST keep the root node in the `Request-Id` and do it's best effort to generate unique suffix to root id.

Parent and local Ids are separated with "." delimiter.

#### Root Parent Id Generation
If `Request-Id` is not provided, it indicates that it's first [instrumented] service to process the operation.
Implementation MUST generate sufficiently large random identifier: e.g. GUID, random 64bit number.

Same considerations are applied to client applications making HTTP requests and generating root request id.

## Correlation-Context
Identifies context of logical operation (transaction, workflow). Operation may involve multiple services interaction and the context should be propagated to all services involved in operation processing.
Every service involved in operation processing may add it's own correlation-context properties.
Correlation-Context is optional.

### Format
Correlation-Context is represented as comma separated list of key value pairs, where each pair is represented in key=value format:

`Correlation-Context: key1=value1, key2=value2`

## Request-Context
Describes caller request context, which caller may optionnaly pass when sends request to downstream service. Upon reception a request with Request-Context, receiver may use this context to enrich telemetry events, but not propagate it to it's children: downstream services requests. Though, receiver may propagate it's own request-context.

### Format
Request-Context is represented as comma separated list of key value pairs, where each pair is represented in key=value format:

`Request-Context: key1=value1, key2=value2`

## Response-Context
Describes receiver response context, which is receiver may optionally include in the response to caller request. Upon reception a response with Response-Context, caller may use this context to enrich telemetry events, but not propagate it to upstream service responses. Though, receiver may send it's own response-context to upstream services.

### Format
Response-Context is represented as comma separated list of key value pairs, where each pair is represented in key=value format:

`Response-Context: key1=value1, key2=value2`

## HTTP Guidelines and Limitations
- [HTTP 1.1 RFC2616](https://tools.ietf.org/html/rfc2616)
- [HTTP Header encoding RFC5987](https://tools.ietf.org/html/rfc5987)
- Practically HTTP header size is limited to several kilobytes (depending on a web server)

# Examples
Let's consider three services: service-a, service-b and service-c. User calls service-a, which calls service-b which in its turn calls service-c.

`User -> service-a -> service-b -> service-c`

Let's also imagine user does not provide any context with it's request.

1. A: receives request it scans through it's headers and does not find `Request-Id`.
2. A: generates a new one: 'abc' (it's not long enough, but helps to understand the scenario).
3. A: adds extra property to `Correlation-Context: sampled=true`
4. A: logs event that operation was started along with Request-Id and `Correlation-Context`
5. A: makes request to service-b:
    * adds extra property to `Request-Context: storageId=1`
    * generates new `Request-Id` by appending try number to the parent request id: abc.1
    * logs that outgoing request is about to be sent with all the available context: `Request-Id: abc.1`, `Correlation-Context: sampled=true` and `Request-Context: storageId=1`
    * sends request to service-b

6. B: service-b receives request
7. B: scans through it's headers and finds `Request-Id: abc.1`, 'Correlation-Context: sampled=true` and 'Request-Context: storageId=1`.
8. B: logs event that operation was started along with all available context
9. B: makes request to service-c:
    * adds extra property to `Request-Context: storageId=2`
    * generates new `Request-Id` by appending try number to the parent request id: abc.1.1
    * logs that outgoing request is about to be sent with all the available context: `Request-Id: abc.1.1`, `Correlation-Context: sampled=true` and `Request-Context: storageId=2`
    * sends request to service-c
    
10. C: service-c receives request, logs and processes it and responds with `Response-Context: storageId=3`
11. B: service-b receives service-c response 
    * logs response with context: `Request-Id: abc.1.1`, `Correlation-Context: sampled=true` and `Request-Context: storageId=2`, `ResponseContext: storageId=3`
    * responds to service-a with `Response-Context: storageId=2`
    
12. A: service-a receives service-a response
    * logs response with context: `Request-Id: abc.1`, `Correlation-Context: sampled=true`, `Request-Context: storageId=1`, `ResponseContext: storageId=2`
    * Responds to caller and may optionally add `Response-Context: Request-Id=abc.1` to inform user about generated requestId or any other context.

# Industry standards
- [Google Dapper tracing system](http://static.googleusercontent.com/media/research.google.com/en//pubs/archive/36356.pdf)
- [Zipkin](http://zipkin.io/)
- [OpenTracing](http://opentracing.io/)
