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
| Correlation-Context   | Optional. Comma separated list of key-value pairs: Id=id, key1=value1, key2=value2 | Operation context which is propagated across all services involved in operation processing |

## Request-Id
`Request-Id` uniquely identifies every HTTP request involved in operation processing. 

Request-Id is generated on the caller side and passed to callee. Implementation should expect to receive `Request-Id` in header of incoming request. 

1. If it's present, implementation MUST generate unique `Request-Id` for every outoing request and pass it to downstream service (supporting this protocol)
2. If it's not present, it may indicate this is the first instrumented service to receive request or this request was not sampled by upstream service and therefore does not have any context associated with it. In this case implementation MAY:
  * generate new `Request-Id` (see [Root Parent Id Generation](#root-parent-id-generation) for generation considerations) for the incoming request, and follow the same path as if `Request-Id` was present initially (see p1)
  
  OR
  * consider this request as is not sampled, so it is not required to generate Request-Ids and propagate them.

It is essential that 'incoming' and 'outgoing' Request-Ids are included in the telemetry events, so implementation of this protocol MUST provide read access to Request-Id for logging systems.

`Request-Id` is required field, which means that every instrumented request MUST have it. If implementation does not find `Request-Id` in the incoming request headers, it should consider it as non-instrumented and MAY not look for `Correlation-Context`.

###  Hierarchical Request-Id
Implementations SHOULD use hierarchical structure for the Id. 

1. If Request-Id is provided from upstream service, implementation SHOULD append small id preceded with separator and pass it to downstream service, making sure every outgoing request has different suffix.
2. If it is not provided and implementation decides to instrument the request,  it MUST generate new `Request-Id` (see [Root Parent Id Generation](#root-parent-id-generation)) to represent incoming request and follow approach described in p1. for outgoing requests.

Thus, Request-Id has path structure and the root node serve as single correlation id, common for all requests involved in operation processing and implementations are ENCOURAGED to follow this approach. 

If implementation chooses not to follow this recommendation, it MUST ensure

1. It provides additional `Id` property in `Correlation-Context` serving as single unique identifier of the whole operation
2. `Request-Id` is unique for every outgoing request made in scope of the same operation

### Request-Id Format
`Request-Id` is a string up to 256 bytes in length.

#### Formatting hierarchical Request-Id
`Request-Id` has following schema:

parentId.localId

ParentId is Request-Id passed from upstream service (or generated if was not provided), it may have hierarchical structure itself.
LocalId is generated to identify internal operation. It may have hierarchical structure considering service or protocol implementation may split operation to multiple activities.
- It MUST be unique for every outgoing HTTP request sent while processing the incoming request. 
- It SHOULD be small to avoid `Request-Id` overflow
- If appending localId to `Request-Id` would cause it to exceed length limit, implementation MUST keep the root node in the `Request-Id` and do its best effort to generate unique suffix to root id.

Parent and local Ids are separated with "." delimiter.

#### Root Parent Id Generation
If `Request-Id` is not provided, it indicates that it's first instrumented service to process the operation or upstream service decided not to sample this request.
If implementation decides to instrument this request flow, it MUST generate sufficiently large random identifier: e.g. GUID, 64bit number.
Same considerations are applied to client applications making HTTP requests and generating root request id.

## Correlation-Context
Identifies context of logical operation (transaction, workflow). Operation may involve multiple services interaction and the context should be propagated to all services involved in operation processing. Every service involved in operation processing may add its own correlation-context properties.

Correlation-Context is optional, which means that it may or may not be provided by upstream service.
Implementation MUST provide access to Correlation-Context for logging systems and MUST support adding properties to Correlation-Context.

If `Correlation-Context` is provided by upstream service, implementation MUST propagate it further to downstream services.

If implementation does not support hierarchical `Request-Id` structure, it MUST provide additional `Id` property in `Correlation-Context` serving as single unique identifier of the whole operation.

### Correlation Id
Many applications and tracing systems implement single correlation id, identifying the whole operation through all services and client applications. Even though, root part of Request-Id may be used for this purpose, having additional field for correlation id could be more efficient for existing tracing systems and query tools.

If implementation needs to pass such correlation id, we ENCOURADGE it to use `Id` property in `Correlation-Context`.

Since it could be problematic to ensure client code always set correlation id (because it's done from browser or client application is hard to change), implementation MAY generate a new Id and add it to the `Correlation-Context` if it's not present in the incoming request, see [Root Parent Id Generation](#root-parent-id-generation) for generation considerations.

### Correlation-Context Format
Correlation-Context is represented as comma separated list of key value pairs, where each pair is represented in key=value format:

`Correlation-Context: Id=correlationId, key1=value1, key2=value2`

## HTTP Guidelines and Limitations
- [HTTP 1.1 RFC2616](https://tools.ietf.org/html/rfc2616)
- [HTTP Header encoding RFC5987](https://tools.ietf.org/html/rfc5987)
- Practically HTTP header size is limited to several kilobytes (depending on a web server)

# Examples
Let's consider three services: service-a, service-b and service-c. User calls service-a, which calls service-b to fulfill the user request

`User -> service-a -> service-b`

Let's also imagine user provides initial `Request-Id: abc`(it's not long enough, but helps to understand the scenario).

1. A: service-a receives request 
  * scans through its headers and finds `Request-Id : abc`.
  * it generates a new Request-Id: `abc.1` to uniquely describe operation within service-a
  * it does not find `Correlation-Context` and Id, so it adds Id property to CorrelationContext `Id=123`
  * logs event that operation was started along with `Request-Id: abc.1` and `Correlation-Context: Id=123`
2. A: service-a makes request to service-b:
  * generates new `Request-Id` by appending try number to the parent request id: `abc.1.1`
  * logs that outgoing request is about to be sent with all the available context: `Request-Id: abc.1.1`, `Correlation-Context: Id=123`
  * sends request to service-b
3. B: service-b receives request
  * scans through its headers and finds `Request-Id: abc.1.1`, `Correlation-Context: Id=123`
  * it generates a new Request-Id: `abc.1.1.1` to uniquely describe operation within service-b
  * logs event that operation was started along with all available context: `Request-Id: abc.1.1.1`, `Correlation-Context: Id=123`
  * processes request and responds to service-a
4. A: service-a receives response from service-b
  * logs response with context: `Request-Id: abc.1.1`, `Correlation-Context: Id=123`
  * Processes request and responds to caller

As a result log records may look like:

| Message  |  Component name | Context |
| ---------| --------------- | ------- |
| user starts request to service-a | user | `Request-Id=abc` |
| incoming request | service-a | `Request-Id=abc.1; Parent-Request-Id=abc; Id=123` |
| request to service-b | service-a | `Request-Id=abc.1.1; Parent-Request-Id=abc.1; Id=123` |
| incoming request | service-b | `Request-Id=abc.1.1.1; Parent-Request-Id=abc.1.1; Id=123` |
| response | service-b | `Request-Id=abc.1.1.1; Parent-Request-Id=abc.1.1; Id=123` |
| response from service-b | service-a | `Request-Id=abc.1.1; Parent-Request-Id=abc.1; Id=123` |
| response | service-a | `Request-Id=abc.1; Parent-Request-Id=abc;  Id=123` |
| response from service-a | user | `Request-Id=abc` |

#### Remarks
* All logs may be queried by Request-id prefix `abc`, all backend logs may also be queried by exact Id match: `123`
* Logs for particular request may be queried by exact Request-Id match
* Every time service receives request, it generates new Request-Id, however it is not a requirement
* It's recommended to add Parent-Request-Id (as Request-Id of parent operation) to the logs to exactly know which operation caused nested operation to start. Even though Request-Id has heirarchical structure, having parent id logged ensures that parent-child relationships of nested operations could be always restored. 

## Non-hierarchical Request-Id example
1. A: service-a receives request 
  * scans through its headers and finds `Request-Id : abc`.
  * generates a new one: `def`
  * adds extra property to CorrelationContext `Id=123`
  * logs event that operation was started along with `Request-Id: def`, `Correlation-Context: Id=123`
2. A: service-a makes request to service-b:
  * generates new `Request-Id: ghi`
  * logs that outgoing request is about to be sent with all the available context: `Request-Id: ghi`, `Correlation-Context: Id=123`
  * sends request to service-b
3. B: service-b receives request
  * scans through its headers and finds `Request-Id: ghi`, `Correlation-Context: Id=123`
  * logs event that operation was started along with all available context: `Request-Id: ghi`, `Correlation-Context: Id=123`
  * processes request and responds to service-a
4. A: service-a receives response from service-b
  * logs response with context: `Request-Id: def`, `Correlation-Context: Id=123`
  * Processes request and responds to caller

As a result log records may look like:

| Message  |  Component Name | Context |
| ---------| --------------- | ------- |
| user starts request to service-a | user | `Request-Id=abc` |
| incoming request | service-a | `Request-Id=def; Parent-Request-Id=abc; Id=123` |
| request to service-b | service-a | `Request-Id=ghi; Parent-Request-Id=def, Id=123` |
| incoming request | service-b | `Request-Id=jkl; Parent-Request-Id=ghi; Id=123` |
| response | service-b |`Request-Id=jkl; Parent-Request-Id=ghi; Id=123` |
| response from service-b | service-a | `Request-Id=ghi; Parent-Request-Id=def; Id=123` |
| response | service-a |`Request-Id=def; Parent-Request-Id=abc; Id=123` |
| response from service-a | user | `Request-Id=abc` |

#### Remarks
* Log retrieval may require several queries, however user may also set correlation id to simplify it. Query may start with Request-Id from user: `select Id where Parent-Request-Id == 123`, which will give correlation Id. Then all logs may be queried for `Request-Id == abc` || `Id == 123`
* Logs for particular request may be queried by exact Request-Id match
* When nested operation starts (outgoing request), request-id of parent operation (incoming request) needs to be logged to find what caused nested operation to start and therefore describe parent-child relationship of operations in logs

## Mixed hierarchical and non-hierarchical scenario
In heterogenious environment, some services may support hierarchical Request-Id generation and others may not.

Requirements listed [Request-Id](#request-id) help to ensure all telemetry for the operation still is accessible:
- if implementation supports hierarchical Request-Id, it MUST propagate `Correlation-Context` and **MAY** add `Id` if missing
- if implementation does NOT support hierarchical Request-Id, it MUST propagate `Correlation-Context` and **MUST** add `Id` if missing

Let's imagine service-a supports hierarchical Request-Id and service-b does not:

1. A: service-a receives request 
  * scans through its headers and finds `Request-Id : abc`.
  * generates a new one: `abc.1`
  * logs event that operation was started along with `Request-Id: abc.1`
2. A: service-a makes request to service-b:
  * generates new `Request-Id: abc.1.1`
  * logs that outgoing request is about to be sent
  * sends request to service-b
3. B: service-b receives request
  * scans through its headers and finds `Request-Id: abc.1.1`
  * generates a new Request-Id: `def`   
  * does not see `Correlation-Context` and adds `Id` property to CorrelationContext `Id=123`
  * logs event that operation was started
  * processes request and responds to service-a
4. A: service-a receives response from service-b
  * logs response with context: `Request-Id: abc.1.1`
  * Processes request and responds to caller

As a result log records may look like:

| Message  |  Component Name | Context |
| ---------| --------------- | ------- |
| user starts request to service-a | user | `Request-Id=abc` |
| incoming request | service-a | `Request-Id=abc.1; Parent-Request-Id=abc` |
| request to service-b | service-a | `Request-Id=abc.1.1; Parent-Request-Id=abc.1` |
| incoming request | service-b | `Request-Id=def; Parent-Request-Id=abc.1.1; Id=123` |
| response | service-b |`Request-Id=def; Parent-Request-Id=abc.1.1; Id=123` |
| response from service-b | service-a | `Request-Id=abc.1.1; Parent-Request-Id=abc.1` |
| response | service-a |`Request-Id=abc.1; Parent-Request-Id=abc` |
| response from service-a | user | `Request-Id=abc` |

#### Remarks
* Retrieving all log records would require several queries: all logs without correlation-id (from service-a and upstream) could be queried by Request-Id prefix, all downstream logs could be queried by Correlation Id. Correlation-Id may be found by Parent-Request-Id query with `abc` prefix. User or implementation may insist on setting correlation id on the first instrumented serivce to simplify retrieval.

# Industry standards
- [Google Dapper tracing system](http://static.googleusercontent.com/media/research.google.com/en//pubs/archive/36356.pdf)
- [Zipkin](http://zipkin.io/)
- [OpenTracing](http://opentracing.io/)
