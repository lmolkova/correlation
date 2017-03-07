# Overview
One of the common problems in microservices development is ability to trace request flow from client (application, browser) through all the services involved in processing.

Typical scenarios include:

1. Tracing error received by user
2. Performance analysis and optimization: whole stack of request needs to be analyzed to find where performance issues come from
3. A/B testing: metrics for requests with experimental features should be distinguished and compared to 'production' data.

These scenarios require every request to carry additional context and services to enrich their telemetry events with this context, so it would possible to correlate telemetry from all services involved in operation processing.

Tracing an operation involves an overhead on application performance and should always be considered as optional, so application may not trace anything, trace only particular operations or some percent of all operations. 
Tracing should be consistent: operation should be either fully traced, or not traced at all.

This document provides guidance on the context needed for telemetry correlation and describes its format in HTTP communication. The context is not specific to HTTP protocol, it represents set of idenitiers that are needed or helpful for end-to-end tracing. Application widely use distributed queues for asyncronous processing so operation may start (or continue) from a queue message; applications should propagate the context through the queues and restore (create) it when they start processing received task.

# HTTP Protocol proposal
| Header name           |  Format    | Description |
| ----------------------| ---------- | ---------- |
| Request-Id            | Required. String | Unique identifier for every HTTP request involved in operation processing |
| Correlation-Context   | Optional. Comma separated list of key-value pairs: Id=id, key1=value1, key2=value2 | Operation context which is propagated across all services involved in operation processing |

## Request-Id
`Request-Id` uniquely identifies every HTTP request involved in operation processing. 

Request-Id is generated on the caller side and passed to callee. 

Implementation of this protocol should expect to receive `Request-Id` in header of incoming request. 
Absence of Request-Id indicates that it is either first instrumented service in the system or this request was not traced by upstream service and therefore does not have any context associated with it.
To start tracing the request, implementation MUST generate new `Request-Id` (see [Root Request Id Generation](#root-request-id-generation)) for the incoming request.

Implementation MUST generate unique `Request-Id` for every outgoing request and pass it to downstream service (supporting this protocol)

`Request-Id` is required field, which means that every instrumented request MUST have it. If implementation does not find `Request-Id` in the incoming request headers, it should consider it as non-traced and MAY not look for `Correlation-Context`.

It is essential that 'incoming' and 'outgoing' Request-Ids are included in the telemetry events, so implementation of this protocol MUST provide read access to Request-Id for logging systems.

### Request-Id Format
`Request-Id` is a string up to 1024 bytes length. It contains only [Base64](https://en.wikipedia.org/wiki/Base64) and "-" (hyphen), "|" (vertical bar), "." (dot), and "_"( underscore) characters.

Vertical bar, dot and underscore are reserved characters that used to mark and delimit heirarchical Request-Id, and must not be present in the nodes. Hyphen may be used in the nodes.

Implementations SHOULD support hierarchical structure for the Request-Id, described in [Hierarchical Request-Id document](hierarchical_request_id.md).
See [Flat Request-Id](flat_request_id.md) for non-hierarchical Request-Id requirements.

## Correlation-Context
Correlation-Context identifies context of logical operation (transaction, workflow). Operation may involve multiple services interaction and the context should be propagated to all services involved in operation processing. Every service involved in operation processing may add its own correlation-context properties. 

Hierarchical Request-Id provides all information essential for telemetry correlation and Correlation-Context may optionally be used by applications to group telemetry based on other properties such as feature flags.
Usage of Correlation-Context involves performance overhead related to extracting, injecting and transmitting it over HTTP, storing the value both in memory and logging system storage. Applications are encouradged to add Correlation-Context properties where it is stongly necessary for telemetry purposes.

`Correlation-Context` is optional, which means that it may or may not be provided by upstream service.

If `Correlation-Context` is provided by upstream service, implementation MUST propagate it further to downstream services.

Implementation MUST provide access to `Correlation-Context` for logging systems and MUST support adding properties to Correlation-Context.

### Correlation-Context Format
`Correlation-Context` is represented as comma separated list of key value pairs, where each pair is represented in key=value format:

`Correlation-Context: key1=value1, key2=value2`

Neither keys nor values MUST NOT contain "="(equals) or "," (comma) characters. 

Keys may be used by logging system as a column names. However it may be useful to have duplicated keys in the Correlation-Context: e.g. when services enable different feature flags and put them into Correlation-Context.

Implementation MUST support receiving duplicated keys in Correlation-Context: it MUST NOT suppress values with duplicated keys. Depending on data structure used for Correlation-Context, implementation MAY either concatenate values with duplicated keys into one value or MAY allow duplicated keys, thus let logging system decide how to represent them.

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
- [Flat Request-Id](flat_request_id.md)