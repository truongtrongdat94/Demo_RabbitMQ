# Environment Setup

## Docker Compose Stack

The root `docker-compose.yml` starts three infrastructure/runtime services:

```text
rabbitmq  - RabbitMQ Management UI, AMQP, and MQTT plugin
renderer  - one-shot RabbitMQ config and definitions renderer
mongodb   - MongoDB document database
node-red  - publisher simulator
consumer  - standalone .NET worker consuming AMQP queues
```

Start the stack:

```bash
cp .env.example .env
docker compose up -d
```

Check service status:

```bash
docker compose ps
```

Follow logs:

```bash
docker compose logs -f rabbitmq
docker compose logs -f node-red
docker compose logs -f consumer-service
```

If the stack was already started with old credentials, RabbitMQ and MongoDB named volumes may keep the previous users. For a clean local rebuild, remove the volumes only when you do not need the stored data.

Node-RED runtime files live in the `node_red_data` named volume. The repository stores only the source flow, settings, and bootstrap scripts.

## RabbitMQ

Open RabbitMQ Management:

```text
http://localhost:15672
```

Login:

```text
Username: value of RABBITMQ_ADMIN_USER in .env
Password: value of RABBITMQ_ADMIN_PASSWORD in .env
```

Expected topology:

```text
Virtual host:      iot
Business exchange: iot.telemetry.exchange
DLX:               iot.telemetry.dlx

factory.telemetry.electricity.# -> telemetry.electricity.queue
factory.telemetry.steam.#       -> telemetry.steam.queue
factory.telemetry.gas.#         -> telemetry.gas.queue

telemetry.electricity.dead -> telemetry.electricity.dlq
telemetry.steam.dead       -> telemetry.steam.dlq
telemetry.gas.dead         -> telemetry.gas.dlq
```

The domain queues and DLQs are declared as quorum queues for stronger data safety than classic queues in production-style deployments.
RabbitMQ 4.3 quorum queues provide native delayed retry, configured directly on the domain queues:

```text
delayed-retry-type = all
delayed-retry-min  = 1000 ms
delayed-retry-max  = 30000 ms
delivery-limit     = 5
```

RabbitMQ users and permissions are not created from `RABBITMQ_DEFAULT_USER` / `RABBITMQ_DEFAULT_PASS`. The stack renders a complete definitions file from:

```text
infra/rabbitmq/definitions.template.json
```

to:

```text
infra/rabbitmq/generated/definitions.json
```

The generated file is ignored by Git because it contains password hashes.

## MongoDB

MongoDB is available on:

```text
mongodb://localhost:27017
```

The consumer service should connect with the application credentials from `.env`:

```text
mongodb://<MONGO_APP_USERNAME>:<MONGO_APP_PASSWORD>@mongodb:27017/<MONGO_APP_DATABASE>
```

## Consumer Service

The consumer service is a standalone .NET 10 Worker Service running in its own container. It subscribes to:

```text
telemetry.electricity.queue
telemetry.steam.queue
telemetry.gas.queue
```

Message outcomes:

```text
ACK                  -> message processed successfully
Reject requeue=false -> invalid schema, routed to DLQ
NACK requeue=true    -> transient failure, handled by quorum delayed retry
```
