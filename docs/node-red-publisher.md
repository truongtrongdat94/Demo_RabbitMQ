# Node-RED Publisher

## Flow Location

The simulator flow is stored at:

```text
simulators/node-red-publisher/flows.json
```

Docker Compose keeps Node-RED runtime state in the `node_red_data` named volume mounted at `/data`. The source files in `simulators/node-red-publisher` are mounted read-only under `/opt/node-red-source` and copied into `/data` when the container starts.

This keeps runtime files such as `flows_cred.json`, `node_modules`, `lib`, and `.config.*` out of the source folder.

MQTT credentials are not stored in `flows.json`. Docker Compose maps the RabbitMQ publisher service account into Node-RED, and the container renders `flows_cred.json` before Node-RED starts:

```text
RABBITMQ_PUBLISHER_USER
RABBITMQ_PUBLISHER_PASSWORD
NODE_RED_CREDENTIAL_SECRET
```

If you edit flows in the Node-RED UI and want to keep those edits as source, export the flow back into `simulators/node-red-publisher/flows.json`. On container restart, the source `flows.json` is copied into `/data` again.

## Run The Simulator

1. Start the stack:

```bash
cp .env.example .env
docker compose up -d
```

2. Open Node-RED:

```text
http://localhost:1880
```

3. Open the `IoT Telemetry Publisher` flow.

4. Click the inject button for each test case:

```text
VALID_SAVE_DB
INVALID_SCHEMA
VALID_NO_SAVE
VALID_FORCE_DB_ERROR
STEAM_VALID
GAS_VALID
```

5. Open RabbitMQ Management and inspect:

```text
Queues > telemetry.electricity.queue
Queues > telemetry.steam.queue
Queues > telemetry.gas.queue
```

6. Open consumer logs:

```bash
docker compose logs -f consumer-service
```

The messages should route through:

```text
MQTT topic -> iot.telemetry.exchange -> domain queue
```

Node-RED publishes MQTT slash-style topics:

```text
factory/telemetry/electricity/valid
factory/telemetry/steam/valid
factory/telemetry/gas/valid
```

RabbitMQ MQTT plugin maps those topics to AMQP routing keys:

```text
factory.telemetry.electricity.valid
factory.telemetry.steam.valid
factory.telemetry.gas.valid
```

## Manual Import Fallback

If Node-RED opens without the flow, import it manually:

1. Node-RED menu.
2. Import.
3. Select file import.
4. Choose `simulators/node-red-publisher/flows.json`.
5. Click Import.
6. Click Deploy.
