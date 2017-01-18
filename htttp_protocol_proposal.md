# Motivation
One of the common problems in microservices development is ability to trace request flow from client (application, browser) through all the services involved in processing.

Typical scenarios include:

1. Tracing error received by user
2. Performance analysis and optimization: whole stack of request needs to be analyzed
3. A/B testing: metrics for requests with experimental features should be distinguished and compared to 'production' data.

Those scenarios require every request to have additional correlation information. This standard describes minimal set of identifiers to be used and their format in HTTP communication.

# HTTP Protocol proposal
Following correlation identifiers  should be passed in HTTP request headers:

| Header name           |  Format                              | Description                                                                                    |
| ----------------------| -------------------------------------- | ---------------------------------------------------------------------------------------------- |
| Correlation-Context   | Comma separated list of properties: Id=\<correlation-id\>, key1=value1, key2=value2 | Identifies operation (transaction, workflow) context, which may involve multiple services interaction. Every property should be propagated to all services involved in operation processing |
| Caller-Context       | Comma separated list of properties: Id=\<request-id\>, key3=value3, key4=value4  | Identifies particular caller request context, which is propagated from caller to callee only |

# Examples

Let's consider two services: service-a and service-b. User calls service-a, which calls service-b to fulfill user request.
## Correlation Id
Having single correlation-id attached to every request and log record would let us distinguish all information related to the operation.
Correlation id is passed along with every request in `Correlation-Context` with key `Id`.

## Request Id
In the real world case, this request may be retried several times or service-a business logic may require multiple calls to service-b.

E.g. service-a tried to call service-b and request timed out and was retried. Logs from service-b for particular correlation-id would contain records for all retries and it would not be possible to distinguish particular request based on correlation id only. So we need to have another identifier, unique for every request, let's call it request-id.

We also need to be able to map incoming call on service-b to outgoing call on service-a, which implies that request-id for service-b should be generated and logged on caller (service-a) side.

Request id only make sense to immediate receiver of request and passed in `Caller-Context` with key `Id`.

## Custom identifiers
Some application/tracing system may require additional identifiers. 
E.g. ApplicationInsights usage model require each service to write telemetry data to its own AppInsights resource and therefore resource identifier of the caller is passed to downstream service. Following this proposal, custom property of `Caller-Context` should be used for this purpose.

[Zipkin](http://zipkin.io/) distributed system uses custom flags to control sampling and pass feature flags to downstream services. Sampling requires the top most service to decide whether the operation should be sampled or not and other services processing it should follow this decision to ensure request is either fully logged or not logged at all. Thus, sampling flag must be passed in `Correlation-Context`.


# Industry standards
- [Google Dapper tracing system](http://static.googleusercontent.com/media/research.google.com/en//pubs/archive/36356.pdf)
- [Zipkin](http://zipkin.io/)
- [OpenTracing](http://opentracing.io/)
