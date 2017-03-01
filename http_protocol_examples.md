This document provides examples for [HTTP protocol proposal](http_protocol_proposal_v1.md) for telemetry correlation.

See also [Hierarchical Request-Id](hierarchical_request_id.md)

# Examples
Let's consider three services: service-a, service-b and service-c. User calls service-a, which calls service-b to fulfill the user request

`User -> service-a -> service-b`

Let's also imagine user provides initial `Request-Id: /abc`(it's not long enough, but helps to understand the scenario).

1. A: service-a receives request 
  * scans through its headers and finds `Request-Id : /abc`.
  * it generates a new Request-Id: `/abc.bcec871c` to uniquely describe operation within service-a
  * logs event that operation was started along with `Request-Id: /abc.bcec871c` and root part of the Request-Id: "abc"
2. A: service-a makes request to service-b:
  * generates new `Request-Id` by appending try number to the parent request id: `/abc.bcec871c.1`
  * logs that outgoing request is about to be sent with all the available context: `Request-Id: /abc.bcec871c.1`, `RootId: abc`
  * sends request to service-b
3. B: service-b receives request
  * scans through its headers and finds `Request-Id: /abc.bcec871c.1`
  * it generates a new Request-Id: `/abc.bcec871c.1.da4e9679` to uniquely describe operation within service-b
  * logs event that operation was started along with all available context: `Request-Id: /abc.bcec871c.1.da4e9679`, `RootId: abc`
  * processes request and responds to service-a
4. A: service-a receives response from service-b
  * logs response with context: `Request-Id: /abc.bcec871c.1`, `RootId: abc`
  * Processes request and responds to caller

As a result log records may look like:

| Message  |  Component name | Context |
| ---------| --------------- | ------- |
| user starts request to service-a | user | `Request-Id=/abc; RootId=abc` |
| incoming request | service-a | `Request-Id=/abc.bcec871c; Parent-Request-Id=/abc; RootId=abc` |
| request to service-b | service-a | `Request-Id=/abc.bcec871c.1; Parent-Request-Id=/abc.bcec871c; RootId=abc` |
| incoming request | service-b | `Request-Id=/abc.bcec871c.1.da4e9679; Parent-Request-Id=/abc.bcec871c.1; RootId=abc` |
| response | service-b | `Request-Id=/abc.bcec871c.1.da4e9679; Parent-Request-Id=/abc.bcec871c.1; RootId=abc` |
| response from service-b | service-a | `Request-Id=/abc.bcec871c.1; Parent-Request-Id=/abc.bcec871c; RootId=abc` |
| response | service-a | `Request-Id=/abc.bcec871c; Parent-Request-Id=/abc; RootId=abc` |
| response from service-a | user | `Request-Id=/abc; RootId=abc` |

#### Remarks
* All logs may be queried by Request-id prefix `/abc` and by RootId `abc` exact match.
* Logs for particular request may be queried by exact Request-Id match
* Note that Parent-Request-Id is logged. If all parties generate hierarchical Ids, it might be redundant, however in case if Request-Id overflow it or mixed Request-Id schema it may be a single way to resore parent-child relationships between operations.

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
* Log retrieval may require several queries, however user may also set correlation id to simplify it. Query may start with Request-Id from user: `select Id where Parent-Request-Id == abc`, which will give correlation Id. Then all logs may be queried for `Request-Id == abc || Id == 123`
* Logs for particular request may be queried by exact Request-Id match
* When nested operation starts (outgoing request), request-id of parent operation (incoming request) needs to be logged to find what caused nested operation to start and therefore describe parent-child relationship of operations in logs

## Mixed hierarchical and non-hierarchical scenario
In heterogenious environment, some services may support hierarchical Request-Id generation and others may not.

Requirements listed [Request-Id](#request-id) help to ensure all telemetry for the operation still is accessible:
- if implementation supports hierarchical Request-Id, it MUST propagate `Correlation-Context` and **MAY** add `Id` if missing
- if implementation does NOT support hierarchical Request-Id, it MUST propagate `Correlation-Context` and **MUST** add `Id` if missing

Let's imagine service-a supports hierarchical Request-Id and service-b does not:

1. A: service-a receives request 
  * scans through its headers and does not find `Request-Id`.
  * generates a new one: `/abc.bcec871c`
  * logs event that operation was started along with `Request-Id: /abc.bcec871c`
2. A: service-a makes request to service-b:
  * generates new `Request-Id: /abc.bcec871c.1`
  * logs that outgoing request is about to be sent
  * sends request to service-b
3. B: service-b receives request
  * scans through its headers and finds `Request-Id: /abc.bcec871c.1`
  * generates a new Request-Id: `def`   
  * does not see `Correlation-Context`. It parses parent Request-Id, extracts root node: `abc` and adds `Id` property to `CorrelationContext : Id=abc`
  * logs event that operation was started
  * processes request and responds to service-a
4. A: service-a receives response from service-b
  * logs response with context: `Request-Id: /abc.bcec871c.1`
  * Processes request and responds to caller

As a result log records may look like:

| Message  |  Component Name | Context |
| ---------| --------------- | ------- |
| incoming request | service-a | `Request-Id=/abc.bcec871c; RootId=abc` |
| request to service-b | service-a | `Request-Id=/abc.bcec871c.1; Parent-Request-Id=/abc.bcec871c; RootId=abc` |
| incoming request | service-b | `Request-Id=def; Parent-Request-Id=/abc.bcec871c.1; Id=abc` |
| response | service-b |`Request-Id=def; Parent-Request-Id=/abc.bcec871c.1; Id=abc` |
| response from service-b | service-a | `Request-Id=/abc.bcec871c.1; Parent-Request-Id=/abc.bcec871c; RootId=abc` |
| response | service-a |`Request-Id=/abc.bcec871c; RootId=abc` |

#### Remarks
* Note, that even if service-b does not **generate** hierarchical Request-Id, it still could benefit from hierarchical structure, by assigning `Correlation-Context: Id` to the root node of Request-Id
* Retrieving all log records then could be done by RootId or Id exact match to `abc`.
* If the first service to process request does not support hierarchical ids, then it sets `Correlation-Context: Id` immediately and it's propagated further and still may be used to query all logs.

## Request-Id overflow
1. Service receives Request-Id `/41372a23-1f07-4617-bf5e-cbe78bf0a84d.1.1.1....1.1234567890` of 127 bytes length.
2. It generates suffix for outgoing request `.1` that causes Request-Id length to become 129 bytes, which exceeds Request-Id length limit.
 * It generates suffix `12a90283` as hex-encoded 4 bytes random integer.
 * It trims out whole last node of the Request-Id (.1234567890) to make room for new suffix. 
 * It generates new Request-Id for outgoing request as `/41372a23-1f07-4617-bf5e-cbe78bf0a84d.1.1.1....1#12a90283`
3. Next service receives request with Request-Id `/41372a23-1f07-4617-bf5e-cbe78bf0a84d.1.1.1....1#12a90283`
 *  Once it needs to generate new Request-Id, it trims out whole last node `#12a90283`, generates new suffix and appends it to the rest of Request-Id:  `/41372a23-1f07-4617-bf5e-cbe78bf0a84d.1.1.1....1#28e93c43`
