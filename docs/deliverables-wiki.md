# IoT Telemetry Pipeline Deliverables Wiki

## 1. Scope

This document describes the primary project deliverable: a QoS 1 MQTT telemetry pipeline backed by RabbitMQ quorum queues and a standalone C# consumer service.

The QoS 0 clean-session device design is treated as an optional extension, not the main assessment path. The required demonstration focuses on routing telemetry data from Node-RED into RabbitMQ, proving that electricity, steam, and gas messages are routed into the correct queues, and showing that the consumer can process those messages.

## 2. What The System Does

Node-RED simulates IoT devices and publishes telemetry messages to RabbitMQ using MQTT QoS 1.

RabbitMQ receives MQTT messages through the MQTT plugin, maps MQTT topics to AMQP routing keys, and routes them through a custom topic exchange:

```text
iot.telemetry.exchange
```

The exchange routes messages into durable quorum queues:

```text
telemetry.electricity.queue
telemetry.steam.queue
telemetry.gas.queue
```

A standalone C# consumer service subscribes to those queues, validates incoming telemetry, persists valid data to MongoDB, and rejects invalid messages into DLQs.

## 3. Runtime Components

| Component | Responsibility | Local Access |
| --- | --- | --- |
| Node-RED | Simulates IoT publishers and sends MQTT QoS 1 telemetry | http://localhost:1880 |
| RabbitMQ 4.3 cluster | MQTT listener, custom exchange, quorum queues, DLQ topology | http://localhost:15672 |
| MongoDB | Stores valid telemetry documents | localhost:27017 |
| C# Consumer Service | Consumes RabbitMQ domain queues and applies processing rules | Docker service: `consumer-service` |

RabbitMQ runs as a three-node local cluster:

| Node | AMQP | Management UI | MQTT |
| --- | --- | --- | --- |
| `rabbitmq-1` | `5672` | `15672` | `1883` |
| `rabbitmq-2` | `5673` | `15673` | `1884` |
| `rabbitmq-3` | `5674` | `15674` | `1885` |

## 4. Primary Architecture

```mermaid
flowchart LR
    NR["Node-RED<br/>IoT data publisher<br/>MQTT QoS 1"] -->|"MQTT PUBLISH<br/>factory/telemetry/...<br/>port 1883"| MQTT["RabbitMQ MQTT Plugin"]
    MQTT -->|"topic mapping<br/>/ becomes ."| EX["iot.telemetry.exchange<br/>topic exchange"]

    EX -->|"factory.telemetry.electricity.#"| EQ["telemetry.electricity.queue<br/>quorum"]
    EX -->|"factory.telemetry.steam.#"| SQ["telemetry.steam.queue<br/>quorum"]
    EX -->|"factory.telemetry.gas.#"| GQ["telemetry.gas.queue<br/>quorum"]

    EQ --> CS["C# Consumer Service<br/>AMQP 0-9-1"]
    SQ --> CS
    GQ --> CS

    CS -->|"valid telemetry<br/>BasicAck"| MDB["MongoDB<br/>telemetry_events"]
    CS -->|"invalid schema<br/>Reject requeue=false"| DLQ["Domain DLQs"]
    CS -->|"transient failure<br/>Reject requeue=true"| RETRY["Quorum delayed retry"]
```

Screenshot placeholder:

```text
[Screenshot: Node-RED flow showing MQTT QoS 1 publisher and telemetry inject nodes]
```

Video placeholder:

```text
[Video/GIF: Inject telemetry in Node-RED -> RabbitMQ queue receives it -> consumer processes it -> MongoDB document is created]
```

## 5. RabbitMQ Topology

The project uses a custom topic exchange instead of using `amq.topic` as the final business exchange:

```text
iot.telemetry.exchange
```

This makes the application routing topology explicit and separates business routing from RabbitMQ's built-in exchanges.

The RabbitMQ topology is rendered from source-controlled templates before broker startup:

```text
infra/rabbitmq/definitions.template.json -> rabbitmq_generated_config:/generated/definitions.json
infra/rabbitmq/rabbitmq.conf.template    -> rabbitmq_generated_config:/generated/rabbitmq.conf
```

RabbitMQ then loads the rendered definitions through:

```text
management.load_definitions = /etc/rabbitmq/generated/definitions.json
```

### Processing Queues

| Queue | Type | Purpose |
| --- | --- | --- |
| `telemetry.electricity.queue` | quorum | Processes electricity telemetry |
| `telemetry.steam.queue` | quorum | Processes steam telemetry |
| `telemetry.gas.queue` | quorum | Processes gas telemetry |

These queues are durable quorum queues. They are the main processing queues consumed by the C# service.

### Dead-Letter Queues

| Queue | Type | Purpose |
| --- | --- | --- |
| `telemetry.electricity.dlq` | quorum | Stores failed electricity messages |
| `telemetry.steam.dlq` | quorum | Stores failed steam messages |
| `telemetry.gas.dlq` | quorum | Stores failed gas messages |

### Queue Arguments

Domain queues use quorum and delayed retry settings:

```text
x-queue-type = quorum
x-quorum-initial-group-size = 3
x-quorum-target-group-size = 3
x-delivery-limit = 5
x-delayed-retry-type = all
x-delayed-retry-min = 1000
x-delayed-retry-max = 30000
```

Screenshot placeholder:

```text
[Screenshot: RabbitMQ Queues page showing telemetry.electricity.queue, telemetry.steam.queue, and telemetry.gas.queue with type quorum]
```

## 6. MQTT Topic Routing

Node-RED publishes slash-style MQTT topics. RabbitMQ MQTT plugin maps slash-separated MQTT topics into dot-separated AMQP routing keys.

| Node-RED MQTT Topic | RabbitMQ Routing Key | Target Queue |
| --- | --- | --- |
| `factory/telemetry/electricity/valid` | `factory.telemetry.electricity.valid` | `telemetry.electricity.queue` |
| `factory/telemetry/electricity/invalid` | `factory.telemetry.electricity.invalid` | `telemetry.electricity.queue` |
| `factory/telemetry/electricity/no-save` | `factory.telemetry.electricity.no-save` | `telemetry.electricity.queue` |
| `factory/telemetry/electricity/db-error` | `factory.telemetry.electricity.db-error` | `telemetry.electricity.queue` |
| `factory/telemetry/steam/valid` | `factory.telemetry.steam.valid` | `telemetry.steam.queue` |
| `factory/telemetry/gas/valid` | `factory.telemetry.gas.valid` | `telemetry.gas.queue` |

RabbitMQ bindings:

```text
factory.telemetry.electricity.# -> telemetry.electricity.queue
factory.telemetry.steam.#       -> telemetry.steam.queue
factory.telemetry.gas.#         -> telemetry.gas.queue
```

This proves that different telemetry domains are routed through the same custom exchange into their designated domain queues.

Screenshot placeholder:

```text
[Screenshot: RabbitMQ exchange iot.telemetry.exchange showing bindings for electricity, steam, and gas]
```

## 7. Data Publisher Simulation

The Node-RED flow is stored at:

```text
simulators/node-red-publisher/flows.json
```

The active Node-RED tab is:

```text
IoT Telemetry Publisher
```

The MQTT output node is configured with QoS 1. This means the MQTT protocol uses PUBACK for at-least-once delivery between Node-RED and RabbitMQ. Node-RED does not need custom code to manually process PUBACK; the MQTT client implementation handles that protocol behavior.

Manual inject test cases:

| Inject Node | Test Case | Expected Result |
| --- | --- | --- |
| Electricity Valid | `VALID_SAVE_DB` | Routed to electricity queue, saved to MongoDB, ACKed |
| Electricity Invalid | `INVALID_SCHEMA` | Routed to electricity queue, rejected into DLQ |
| Electricity No Save | `VALID_NO_SAVE` | Routed to electricity queue, validated, ACKed without MongoDB write |
| Electricity DB Error | `VALID_FORCE_DB_ERROR` | Routed to electricity queue, transient retry path |
| Steam Valid | `STEAM_VALID` | Routed to steam queue, saved to MongoDB, ACKed |
| Gas Valid | `GAS_VALID` | Routed to gas queue, saved to MongoDB, ACKed |

Screenshot placeholder:

```text
[Screenshot: Node-RED inject nodes for valid, invalid, no-save, db-error, steam, and gas test cases]
```

## 8. Consumer Application

The consumer is a standalone C# service located at:

```text
apps/consumer-service
```

It connects to RabbitMQ over AMQP 0-9-1 and subscribes to three domain queues:

```text
telemetry.electricity.queue
telemetry.steam.queue
telemetry.gas.queue
```

The consumer uses one connection and separate channels for the subscribed queues.

Before applying a test-case action, the consumer validates both the telemetry schema and the demo control contract. Required payload fields include:

```text
MessageId
CorrelationId
Source
SchemaVersion
MessageType
ID_Gateway
Timestamp_Gateway
Data_Gateway[].Topic
Data_Gateway[].Data_Devices[].ID_Device
Data_Gateway[].Data_Devices[].Type_Device
Data_Gateway[].Data_Devices[].Timestamp_Device
Data_Gateway[].Data_Devices[].Reading_Device
Meta.TestCase
Simulate.NoSave
Simulate.ForceDbError
```

`Meta.TestCase` selects the assignment scenario. `Simulate.NoSave` and `Simulate.ForceDbError` must match that scenario before the message is saved, skipped, or retried. For `INVALID_SCHEMA`, the expected outcome is that schema validation fails before the test-case action is applied.

```mermaid
flowchart TD
    MSG["Message received from RabbitMQ"] --> PARSE["Parse JSON"]
    PARSE --> VALIDATE["Validate telemetry schema<br/>gateway, devices, readings"]
    VALIDATE --> CONTRACT["Validate Meta.TestCase<br/>and Simulate flags"]
    CONTRACT --> CASE["Apply test-case action"]

    CASE -->|"VALID_SAVE_DB / STEAM_VALID / GAS_VALID"| SAVE["Upsert telemetry document into MongoDB"]
    SAVE --> ACK["BasicAck"]

    CASE -->|"VALID_NO_SAVE<br/>Simulate.NoSave=true"| NOSAVE["Log payload only"]
    NOSAVE --> ACK

    CASE -->|"INVALID_SCHEMA / unsupported case"| PERM["Permanent failure"]
    PERM --> REJECT_FALSE["BasicReject requeue=false"]
    REJECT_FALSE --> DLQ["DLQ"]

    CASE -->|"VALID_FORCE_DB_ERROR<br/>Simulate.ForceDbError=true"| TRANSIENT["Simulated transient failure"]
    TRANSIENT --> REJECT_TRUE["BasicReject requeue=true"]
    REJECT_TRUE --> RETRY["Quorum delayed retry"]
```

Expected consumer logs:

```text
Persisted telemetry message <MessageId> from queue telemetry.electricity.queue
Rejecting permanent failure from queue telemetry.electricity.queue
Rejecting transient failure from queue telemetry.electricity.queue; requeue=true
```

Screenshot placeholder:

```text
[Screenshot: consumer-service logs showing persisted, rejected, and retried messages]
```

## 9. MongoDB Persistence

Valid telemetry messages are saved into MongoDB:

```text
database: iot_telemetry
collection: telemetry_events
```

The consumer upserts by `MessageId`, which makes repeated delivery safer for demonstration because duplicated QoS 1 messages do not create duplicate documents with the same message identity.

Screenshot placeholder:

```text
[Screenshot: MongoDB telemetry_events collection showing a saved telemetry document]
```

## 10. Security And Production-Style Choices

| Area | Implementation |
| --- | --- |
| MQTT authentication | Anonymous MQTT access is disabled |
| RabbitMQ users | Separate users for admin, publisher, and consumer |
| RabbitMQ topology | Definitions are rendered from source-controlled templates and loaded at broker startup |
| RabbitMQ passwords | Password hashes are generated with `rabbitmqctl hash_password` |
| Node-RED credentials | `flows_cred.json` is generated at startup and not committed |
| MongoDB authentication | MongoDB root and app users are configured through env and init script |
| Queue durability | Processing queues and DLQs are quorum queues |
| Permissions | Publisher can write telemetry topics; consumer can read only domain queues |

## 11. Runbook

For the full presentation script, use:

```text
docs/live-demo.md
```

Start the stack:

```bash
docker compose up -d
```

Open UIs:

```text
Node-RED: http://localhost:1880
RabbitMQ: http://localhost:15672
```

Check queues:

```bash
docker compose exec rabbitmq-1 rabbitmqctl list_queues -p iot name type consumers messages state
```

Check AMQP connections:

```bash
docker compose exec rabbitmq-1 rabbitmqctl list_connections name protocol user vhost channels state
```

Watch consumer logs:

```bash
docker compose logs -f consumer-service
```

Watch Node-RED logs:

```bash
docker compose logs -f node-red
```

## 12. Demonstration Checklist

| Step | Evidence To Capture | Status |
| --- | --- | --- |
| Start the stack | `docker compose up -d` completes | Pending evidence |
| Show architecture | Mermaid diagram or exported image | Pending evidence |
| Show publisher | Node-RED flow with MQTT QoS 1 output | Pending evidence |
| Prove topic routing | RabbitMQ exchange bindings and domain queues | Pending evidence |
| Prove electricity route | Electricity message appears in `telemetry.electricity.queue` | Pending evidence |
| Prove steam route | Steam message appears in `telemetry.steam.queue` | Pending evidence |
| Prove gas route | Gas message appears in `telemetry.gas.queue` | Pending evidence |
| Prove consumer subscription | Queue list shows consumers on the three domain queues | Pending evidence |
| Prove valid persistence | Consumer log and MongoDB document | Pending evidence |
| Prove invalid DLQ | Invalid message reaches `telemetry.electricity.dlq` | Pending evidence |
| Prove transient retry | DB error test follows quorum delayed retry behavior | Pending evidence |
| Prove quorum type | Queue list shows `telemetry.*.queue` as `quorum` | Pending evidence |

Video placeholder:

```text
[Video/GIF: Node-RED inject -> RabbitMQ domain queue -> C# consumer log -> MongoDB document]
```

## 13. Optional Extension: QoS 0 Clean-Session Device

QoS 0 is not the main task requirement in this document. It is an optional design extension for a different use case: lightweight command delivery to online MQTT devices.

In that model:

```text
AMQP or MQTT publisher -> iot.telemetry.exchange -> MQTT QoS 0 subscriber
```

RabbitMQ can create a runtime MQTT QoS 0 subscription queue for a clean-session MQTT subscriber:

```text
mqtt-subscription-<client-id>qos0
```

This queue is not a backend processing queue and should not be used by the C# AMQP consumer. If an AMQP consumer also needs the same command stream, it should use a separate classic or quorum queue bound to the same routing key.

Comparison:

| Path | Main Consumer | Queue Type | Use Case |
| --- | --- | --- | --- |
| QoS 1 telemetry processing | C# AMQP consumer | quorum | Reliable telemetry processing, retry, DLQ, MongoDB persistence |
| QoS 0 clean-session device | MQTT subscriber | MQTT QoS 0 queue | Lightweight command delivery to online devices |

The QoS 0 design is useful to discuss as an extension, but the deliverable demonstration should lead with the QoS 1 telemetry pipeline because that is the core requirement.

## 14. Design Decisions

### Custom Topic Exchange

The system uses `iot.telemetry.exchange` instead of using `amq.topic` directly. This makes the exchange part of the application contract and keeps routing rules explicit.

### Domain Queues

Electricity, steam, and gas have separate queues. This makes the routing proof clear and allows future scaling or failure isolation per telemetry domain.

### Quorum Queues

Quorum queues are used for processing queues because telemetry data should survive node failures and support delayed retry/DLQ behavior.

### Standalone Consumer Service

The C# service is standalone from RabbitMQ and MongoDB. It connects through Docker networking, subscribes to multiple domain queues, and owns the application-level processing rules.

### QoS 0 As An Extension

QoS 0 is documented separately because it solves a different problem. It is good for online device command delivery, not for durable telemetry processing.
