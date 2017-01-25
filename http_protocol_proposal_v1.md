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

### Formatting hierarchical Request-Id
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

## HTTP Guidelines and Limitations
- [HTTP 1.1 RFC2616](https://tools.ietf.org/html/rfc2616)
- [HTTP Header encoding RFC5987](https://tools.ietf.org/html/rfc5987)
- Practically HTTP header size is limited to several kilobytes (depending on a web server)

# Examples
Let's consider three services: service-a, service-b and service-c. User calls service-a, which calls service-b to fullfill the user request

`User -> service-a -> service-b`

Let's also imagine user provides initial `Request-Id: abc`(it's not long enough, but helps to understand the scenario).

1. A: service-a receives request 
  * scans through it's headers and does finds `Request-Id : abc`.
  * it generates a new Reqiest-Id: `abc.1` to uniquely describe operation within service-a
  * adds extra property to CorrelationContext `sampled=true`
  * logs event that operation was started along with `Request-Id: abc.1` and `Correlation-Context: sampled=true`
2. A: service-a makes request to service-b:
  * generates new `Request-Id` by appending try number to the parent request id: `abc.1.1`
  * logs that outgoing request is about to be sent with all the available context: `Request-Id: abc.1.1`, `Correlation-Context: sampled=true`
  * sends request to service-b
3. B: service-b receives request
   * scans through it's headers and finds `Request-Id: abc.1.1`, `Correlation-Context: sampled=true`
   * it generates a new Request-Id: `abc.1.1.1` to uniquely describe operation within service-b
   * logs event that operation was started along with all available context: `Request-Id: abc.1.1.1`, `Correlation-Context: sampled=true`
   * processes request and responds to service-a
4. A: service-a receives response from service-b
    * logs response with context: `Request-Id: abc.1.1`, `Correlation-Context: sampled=true`
    * Processes request and responds to caller

As a result log records may look like:

| Message  |  Component name | Context |
| ---------| --------------- | ------- |
| user starts request to service-a | user | `Request-Id=abc` |
| incoming request | service-a | `Request-Id=abc.1; Parent-Request-Id=abc; sampled=true` |
| request to service-b | service-a | `Request-Id=abc.1.1; Parent-Request-Id=abc.1; sampled=true` |
| incoming request | service-b | `Request-Id=abc.1.1.1; Parent-Request-Id=abc.1.1; sampled=true` |
| response | service-b | `Request-Id=abc.1.1.1; Parent-Request-Id=abc.1.1; sampled=true` |
| response from service-b | service-a | `Request-Id=abc.1.1; Parent-Request-Id=abc.1; sampled=true` |
| response | service-a | `Request-Id=abc.1; Parent-Request-Id=abc; sampled=true` |
| response from service-a | user | `Request-Id=abc` |

#### Remarks
* All logs may be queried by Request-id prefix `abc`
* Logs for particular request may be queried by exact Request-Id match
* Every time service receives request, it generates new Request-Id, however it is not a requirement
* It's recommended to add Parent-Request-Id (as Request-Id of parent operation) to the logs to exactly know which operation caused nested operation to start. Even though Request-Id has heirarchical structure, having parent id logged ensures that parent-child relationships of nested operations could be always restored. 

## Non-hierarchical Request-Id example
1. A: service-a receives request 
  * scans through it's headers and finds `Request-Id : abc`.
  * generates a new one: `def`
  * adds extra property to CorrelationContext `CorrelationId=id1`
  * logs event that operation was started along with `Request-Id: def`, `Correlation-Context: CorrelationId=id1`
2. A: service-a makes request to service-b:
  * generates new `Request-Id: ghi`
  * logs that outgoing request is about to be sent with all the available context: `Request-Id: ghi`, `Correlation-Context: CorrelationId=id1`
  * sends request to service-b
3. B: service-b receives request
   * scans through it's headers and finds `Request-Id: ghi`, `Correlation-Context: CorrelationId=id1`
   * logs event that operation was started along with all available context: `Request-Id: ghi`, `Correlation-Context: CorrelationId=id1`
   * processes request and responds to service-a
4. A: service-a receives response from service-b
    * logs response with context: `Request-Id: def`, `Correlation-Context: CorrelationId=id1`
    * Processes request and responds to caller

As a result log records may look like:

| Message  |  Component Name | Context |
| ---------| --------------- | ------- |
| user starts request to service-a | user | `Request-Id=abc` |
| incoming request | service-a | `Request-Id=def; Parent-Request-Id=abc; CorrelationId=id1` |
| request to service-b | service-a | `Request-Id=ghi; Parent-Request-Id=def, CorrelationId=id1` |
| incoming request | service-b | `Request-Id=jkl; Parent-Request-Id=ghi; CorrelationId=id1` |
| response | service-b |`Request-Id=jkl; Parent-Request-Id=ghi; CorrelationId=id1` |
| response from service-b | service-a | `Request-Id=ghi; Parent-Request-Id=def; CorrelationId=id1` |
| response | service-a |`Request-Id=def; Parent-Request-Id=abc; CorrelationId=id1` |
| response from service-a | user | `Request-Id=abc` |

#### Remarks
* All logs may be queried by Correlation-Id `abc`
* Logs for particular request may be queried by exact Request-Id match
* When nested operation starts (outgoing request), request-id of parent operation (incoming request) needs to be logged to find what cased nested operation to start and therefore describe parent-child relationship of operations in logs

# Industry standards
- [Google Dapper tracing system](http://static.googleusercontent.com/media/research.google.com/en//pubs/archive/36356.pdf)
- [Zipkin](http://zipkin.io/)
- [OpenTracing](http://opentracing.io/)
