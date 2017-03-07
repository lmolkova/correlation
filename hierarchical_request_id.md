#  Hierarchical Request-Id
This document describes hierarchical Request-Id schema for [HTTP protocol proposal](http_protocol_proposal_v1.md) for telemetry correlation.

## Overview
The main requirement for Request-Id is uniqueness, any two requests processed by the cluster must not collide.
Guids or big random number help to achieve it, but they require other identifiers to query all requests related to the operation.

Hierarchical Request-Id look like `/<root-id>.<local-id1>.<local-id2>.` (e.g. `/9e74f0e5-efc4-41b5-86d1-3524a43bd891.bcec871c_1.`) and holds all information needed to trace whole operation and particular request.
Root-id serves as common identifier for all requests involved in operation processing and local-ids represent internal activities (and requests) done within scope of this operation.

[CorrelationVector](https://osgwiki.com/wiki/CorrelationVector) is valid Hierarchical Request-Id, except it does not start with "/". Implementation SHOULD allow other schemas for incoming request identifiers.

### Formatting Hierarchical Request-Id
If `Request-Id` was not provided from upstream service and implementation decides to trace the request, it MUST generate new `Request-Id` (see [Root Request Id Generation](http_protocol_proposal_v1.md#root-request-id-generation)) to represent incoming request. 

In heterogenious environment implementations of this protocol with hierarchical `Request-Id` may interact with other services that do not implement this protocol, but still have notion of request Id. Implementation or logging system should be able unambiguously identify if given `Request-Id` has hierarchical schema. 

Therefore every implementation which support hierarchical structure MUST prepend "/" symbol to generated `Request-Id`.

It also MUST append "." (dot) to the end of generated Request-Id to unambiguously mark end of it (e.g. search for `/123` may return `/1234`, but search for `/123.` would be exact)

#### Incoming Request
When Request-Id is provided by upstream service, there is no guarantee that it is unique within the entire system. 

Implementation SHOULD make it unique by adding small suffix to incoming Request-Id to represent internal activity and use it for outgoing requests.
If implementation does not trust incoming Request-Id in the least, suffix may be as long as [Root Request Id](http_protocol_proposal_v1.md#root-request-id-generation).
We recommend appending random string of 8 characters length (e.g. 32-bit hex-encoded random integer).

Implementation MUST append "_" (underscore) to mark the end of generated Request-Id.

#### Outgoing Request
When making request to downstream service, implementation MUST append small id to the Request-Id generated to represent this incoming request and pass a new Request-Id to downstream service.

- Suffix MUST be unique for every outgoing HTTP request sent while processing the incoming request; number of request within the scope of this incoming request, may be a good candidate. 
- Suffix MUST contain only A-Z, a-z, 0-9, "-"(hyphen) characters.

Implementation MUST append "." (dot) to mark the end of generated Request-Id.

## Example
Let's consider three services: service-a, service-b and service-c. User calls service-a, which calls service-b to fulfill the user request

`User -> service-a -> service-b`

1. A: service-a receives request 
  * does not find `Request-Id` and generates a new root Request-Id `/Guid.`
  * trace that incoming request was started along with `Request-Id: /Guid.`
2. A: service-a makes request to service-b:
  * generates new `Request-Id` by appending request number to the parent request id: `/Guid.1.`
  * logs that outgoing request is about to be sent with all the available context: `Request-Id: /Guid.1.`
  * sends request to service-b
3. B: service-b receives request
  * scans through its headers and finds `Request-Id: /Guid.1.`
  * it generates a new Request-Id: `/Guid.1.da4e9679_` to uniquely describe operation within service-b
  * logs event that operation was started along with all available context: `Request-Id: /Guid.1.da4e9679_`
  * processes request and responds to service-a
4. A: service-a receives response from service-b
  * logs response with context: `Request-Id: /Guid.1.`
  * Processes request and responds to caller

As a result log records may look like:

| Message  |  Component name | Context |
| ---------| --------------- | ------- |
| user starts request to service-a | user |  |
| incoming request | service-a | `Request-Id=/Guid.` |
| request to service-b | service-a | `Request-Id=/Guid.1.` |
| incoming request | service-b | `Request-Id=/Guid.1.da4e9679_` |
| response | service-b | `Request-Id=/Guid.1.da4e9679_` |
| response from service-b | service-a | `Request-Id=/Guid.1.` |
| response | service-a | `Request-Id=/Guid.` |
| response from service-a | user |  |

### Remarks
* All operation logs may be queried by Request-Id prefix `/Guid.`, logs for particular request may be queried by exact Request-Id match
* When service-a generates a new Request-Id, it does not append suffix, since it generates a root Request-Id and ensures its uniqueness

# See also
- [Flat Request-Id](flat_request_id.md)
