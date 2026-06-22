# IoT Data Pipeline

Local development stack for simulating IoT telemetry ingestion with Node-RED, RabbitMQ MQTT/AMQP, and MongoDB.

## Architecture

```text
Node-RED Publisher
        |
        | MQTT :1883
        v
RabbitMQ MQTT Plugin
        |
        | iot.telemetry.exchange
        v
domain queues
        |
        | AMQP consumer service
        v
MongoDB
```

The consumer is a standalone .NET 10 Worker Service packaged as its own container.

## Services

| Service | URL / Port | Purpose |
| --- | --- | --- |
| Node-RED | http://localhost:18080 | Data publisher simulator |
| RabbitMQ Management | http://localhost:15672 | Broker UI |
| RabbitMQ MQTT | localhost:11883 | MQTT ingestion endpoint |
| RabbitMQ AMQP | localhost:5672 | Consumer connection endpoint |
| MongoDB | localhost:27017 | Telemetry persistence |
| Consumer Service | container only | AMQP consumer and MongoDB writer |

RabbitMQ Management login:

```text
Username: value of RABBITMQ_ADMIN_USER in .env
Password: value of RABBITMQ_ADMIN_PASSWORD in .env
```

## Run

```bash
cp .env.example .env
docker compose up -d
docker compose ps
```

Open Node-RED at http://localhost:18080 and trigger the provided inject nodes.
Node-RED MQTT credentials are rendered into `flows_cred.json` from the RabbitMQ publisher service account in `.env` when the container starts.
Node-RED runtime state is stored in the Docker named volume `node_red_data`; the repository keeps only source flow/config files.
The consumer service starts with the stack and subscribes to all telemetry domain queues.

RabbitMQ topology, users, permissions, and topic permissions are rendered from `infra/rabbitmq/definitions.template.json` into the Docker named volume `rabbitmq_generated_config` before RabbitMQ starts. RabbitMQ loads that generated definitions file at broker startup through `management.load_definitions`. Generated files contain password hashes and do not live in the repository source tree.

If credentials or topology were changed after a previous run, RabbitMQ and MongoDB named volumes may still contain old users/data. Recreate volumes only when the stored data is disposable.

## Routing

RabbitMQ loads the following topology into the `iot` virtual host:

```text
iot.telemetry.exchange -- factory.telemetry.electricity.# --> telemetry.electricity.queue
iot.telemetry.exchange -- factory.telemetry.steam.#       --> telemetry.steam.queue
iot.telemetry.exchange -- factory.telemetry.gas.#         --> telemetry.gas.queue

iot.telemetry.dlx -- telemetry.electricity.dead --> telemetry.electricity.dlq
iot.telemetry.dlx -- telemetry.steam.dead       --> telemetry.steam.dlq
iot.telemetry.dlx -- telemetry.gas.dead         --> telemetry.gas.dlq
```

The domain queues are quorum queues with native delayed retry enabled:

```text
retry type: all
min delay:  1000 ms
max delay:  30000 ms
delivery limit before DLQ: 5
```

The Node-RED simulator publishes these test topics:

```text
factory/telemetry/electricity/valid
factory/telemetry/electricity/invalid
factory/telemetry/electricity/no-save
factory/telemetry/electricity/db-error
factory/telemetry/steam/valid
factory/telemetry/gas/valid
```

RabbitMQ MQTT plugin translates MQTT `/` topic separators into AMQP `.` routing keys before routing through `iot.telemetry.exchange`.

## Consumer Behavior

The .NET consumer subscribes to:

```text
telemetry.electricity.queue
telemetry.steam.queue
telemetry.gas.queue
```

Processing policy:

```text
valid save cases       -> validate, upsert into MongoDB, ACK
VALID_NO_SAVE          -> validate, log, ACK
INVALID_SCHEMA         -> reject requeue=false, route to DLQ
VALID_FORCE_DB_ERROR   -> reject requeue=true, let quorum delayed retry handle it
unexpected/transient   -> reject requeue=true, let quorum delayed retry handle it
```

The processor validates the top-level telemetry envelope and nested gateway/device structure before applying the test-case action. `Meta.TestCase` selects the scenario required by the assignment, while `Simulate.NoSave` and `Simulate.ForceDbError` must match that scenario.

## RabbitMQ Access Model

RabbitMQ uses separate service accounts:

```text
RABBITMQ_ADMIN_USER     -> Management UI administrator
RABBITMQ_PUBLISHER_USER -> Node-RED MQTT publisher
RABBITMQ_CONSUMER_USER  -> .NET AMQP consumer
```

The publisher can write only to `iot.telemetry.exchange` and only to telemetry topic routing keys. The consumer can read only from the three domain queues.
