# Motivation
One of the common problems in microservices development is ability to trace request flow from client (application, browser) through all the services involved in processing.

Typical scenarios include:

1. Tracing error received by user
2. Performance analysis and optimization: whole stack of request needs to be analyzed to find where performance issues come from
3. A/B testing: metrics for requests with experimental features should be distinguished and compared to 'production' data.

These scenarios require every request to carry additional context and services to enrich their telemetry events with this context, so it would possible to correlate telemetry from all services involved in operation processing.

This proposal describes context and it's format in HTTP communication.

# HTTP Protocol proposal
| Header name           |  Format    | Description |
| ----------------------| ---------- | ---------- |
| Request-Id            | Required. String | Unique identifier for every HTTP request involved in operation processing |
| Correlation-Context   | Optional. Comma separated list of key-value pairs: Id=id, key1=value1, key2=value2 | Operation context which is propagated across all services involved in operation processing |

## Request-Id
`Request-Id` uniquely identifies every HTTP request involved in operation processing. 

Request-Id is generated on the caller side and passed to callee. 

Implementation should expect to receive `Request-Id` in header of incoming request. 
If it's not present, it may indicate this is the first instrumented service to receive request or this request was not sampled by upstream service and therefore does not have any context associated with it. In this case implementation MUST:
  * generate new `Request-Id` (see [Root Request Id Generation](#root-request-id-generation)) for the incoming request
  OR
  * consider this request as is not sampled, so it is not required to generate Request-Ids and propagate them.

Implementation MUST generate unique `Request-Id` for every outgoing request and pass it to downstream service (supporting this protocol)

It is essential that 'incoming' and 'outgoing' Request-Ids are included in the telemetry events, so implementation of this protocol MUST provide read access to Request-Id for logging systems.

`Request-Id` is required field, which means that every instrumented request MUST have it. If implementation does not find `Request-Id` in the incoming request headers, it should consider it as non-instrumented and MAY not look for `Correlation-Context`.

Implementations SHOULD support hierarchical structure for the Request-Id, described in [Hierarchical Request-Id document](hierarchical_request_id.md).

### Request-Id Format
`Request-Id` is a string up to 128 bytes in length inclusively.
It contains only [Base64](https://en.wikipedia.org/wiki/Base64), "."(dot), "#"(pound) and "-"(dash) characters.

#### Root Request Id Generation
If `Request-Id` is not provided, it indicates that it's first instrumented service to process the operation or upstream service decided not to sample this request.

If implementation decides to instrument this request flow, it MUST generate sufficiently large identifier: e.g. GUID, 64bit or 128 bit random number.

Root Request-Id MUST contain only [Base64 characters](https://en.wikipedia.org/wiki/Base64) and "-". 

If implementation does not support hierarchical `Request-Id` generation, it MUST NOT start Request-Id with "/".

Same considerations are applied to client applications making HTTP requests and generating root Request-Id.

## Correlation-Context
Identifies context of logical operation (transaction, workflow). Operation may involve multiple services interaction and the context should be propagated to all services involved in operation processing. Every service involved in operation processing may add its own correlation-context properties.

`Correlation-Context` is optional, which means that it may or may not be provided by upstream service.

If `Correlation-Context` is provided by upstream service, implementation MUST propagate it further to downstream services.

Implementation MUST provide access to `Correlation-Context` for logging systems and MUST support adding properties to Correlation-Context.

If implementation does not support hierarchical `Request-Id` structure, it MUST ensure `Correlation-Context` has `Id` property serving as single unique identifier of the whole operation and generate one if missing.
 
### Correlation-Context Format
`Correlation-Context` is represented as comma separated list of key value pairs, where each pair is represented in key=value format:

`Correlation-Context: Id=correlationId, key1=value1, key2=value2`

### Correlation Id
Many applications and tracing systems use single correlation id to identify whole operation through all services and client applications. Root part of hierarchical` Request-Id` may be used for this purpose.

In case of heterogenious environment (where some services generate hierarchical Request-Ids and others generate flat Ids) having single identifier, common for all requests, helps to make telemetry query simple and efficient.

Implementations MUST use `Id` property in `Correlation-Context` if they need propagate correlation id across the cluster. 

If implementation does not support hierarchical Request-Id generation, it MUST ensure `Id` is present in `Correlation-Context` or [generate](#correlation-id-generation) new one and add to the `Correlation-Context`.

#### Correlation Id generation
If implementation needs to add `Id` property to `Correlation-Context`:
* SHOULD use root node of the Request-Id received from upstream service if it has hierarchical structure.
* MUST follow [Root Request Id Generation](#root-request-id-generation) rules otherwise

# HTTP Guidelines and Limitations
- [HTTP 1.1 RFC2616](https://tools.ietf.org/html/rfc2616)
- [HTTP Header encoding RFC5987](https://tools.ietf.org/html/rfc5987)
- Practically HTTP header size is limited to several kilobytes (depending on a web server)

# Industry standards
- [Google Dapper tracing system](http://static.googleusercontent.com/media/research.google.com/en//pubs/archive/36356.pdf)
- [Zipkin](http://zipkin.io/)
- [OpenTracing](http://opentracing.io/)

# See also
- [Hierarchical Request-Id](hierarchical_request_id.md)
- [Examples](http_protocol_examples.md)
