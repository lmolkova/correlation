This protocol extends [Correlation HTTP protocol proposal](https://github.com/lmolkova/correlation/blob/master/http_protocol_proposal_v1.md).

# Motivation
In addition to [V1](https://github.com/lmolkova/correlation/blob/master/http_protocol_proposal_v1.md) scenarios, we want to provide mechanism for services to send context to it's immediate receiver or pass back context to caller. Real world examples include:
* Distributed telemetry storage: additionally to Request-Id (defined in [V1](https://github.com/lmolkova/correlation/blob/master/http_protocol_proposal_v1.md)), user needs to query all telemetry storage tenants where logs for this operation may be. Tracing systems may visualize application maps based on the extended properties in caller and callee logs. To support this scenario, tenant identifier of caller may be passed to callee in request and calle may include it's tenant id into the response.
* Log query optimization: applications may add redundunt information into their telemetry to avoid querying telemetry storage multiple times and/or perform joins which may be expensive operations. In some cases, when services belong to multiple organizations, it could be hard or impossible to get all parts of telemtery. In addition to Request-Id and Correlation-Context, services may need to exchange with the context that makes sense to immediate receiver only such as sender/receiver version, device/service type, user/initiator details.
* Availability testing: querying telemetry events may take a long time, it's also hard to automate. To help immediate and automated root cause analysis services may include extended detailed status in response to the request.

# Extension Proposal 
| Header name           |  Format    | Description |
| ----------------------| ---------- | ---------- |
| Request-Context       | Optional **request** header. Comma separated list of key-value pairs: key1=value1, key2=value2 | Context which is passed from caller in **request** to callee  and **not** propagated further | 
| Response-Context      | Optional **response** header. Comma separated list of key-value pairs: key3=value3, key4=value4 | Context which is passed from callee in **response** to caller request and **not** propagated further | 

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

# Examples
User calls service-a, which calls service-b:
`User -> service-a -> service-b`

1. A receives request from user, it contains `Request-Context: tenantId=1`.
2. A calls service-b, it does not propagate Request-Context from user, instead it passes its own context in `Request-Context: tenantId=2` header
3. B receives request from service-a, logs request with all available context including `Request-Context: tenantId=2`
8. B responds to A with it's own tenantId in `Response-Context: tenantId=3` header
9. A responds to user, it includes `Response-Context: tenantId=2` header

Log records for this example may look like:

| Message  |  Component name | Context |
| ---------| --------------- | ------- |
| request to service-a | user | - |
| incoming request | service-a | `Parent-Tenant-Id=1` |
| request to service-b | service-a | `Parent-Tenant-Id=1` |
| incoming request | service-b | `Parent-Tenant-Id=2` |
| response from service-b | service-a | `Parent-Tenant-Id=1; Child-Tenant-Id=3` |
| response from service-a | user | `Child-Tenant-Id=2` |

Note, Request-Id described [V1](https://github.com/lmolkova/correlation/blob/master/http_protocol_proposal_v1.md) is ommited for simplicity.
