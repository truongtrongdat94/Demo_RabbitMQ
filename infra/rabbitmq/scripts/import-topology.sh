#!/bin/sh
set -eu

require_env() {
    name="$1"
    eval "value=\${$name:-}"
    if [ -z "$value" ]; then
        echo "$name is required" >&2
        exit 1
    fi
}

require_env RABBITMQ_ADMIN_USER
require_env RABBITMQ_ADMIN_PASSWORD
require_env RABBITMQ_VHOST

export RABBITMQADMIN_TARGET_HOST="${RABBITMQ_MANAGEMENT_HOST:-rabbitmq-1}"
export RABBITMQADMIN_TARGET_PORT="${RABBITMQ_MANAGEMENT_PORT:-15672}"
export RABBITMQADMIN_TARGET_VHOST="$RABBITMQ_VHOST"
export RABBITMQADMIN_USERNAME="$RABBITMQ_ADMIN_USER"
export RABBITMQADMIN_PASSWORD="$RABBITMQ_ADMIN_PASSWORD"

expected_nodes="${RABBITMQ_CLUSTER_EXPECTED_NODES:-3}"
definitions_file="${RABBITMQ_DEFINITIONS_FILE:-/generated/definitions.json}"
attempt=1
max_attempts="${RABBITMQ_CLUSTER_WAIT_ATTEMPTS:-60}"

cleanup_obsolete_electricity_topology() {
    for shard in 0 1 2; do
        rabbitmqadmin -q --non-interactive bindings delete \
            --source iot.telemetry.exchange \
            --destination-type queue \
            --destination "telemetry.electricity.shard-$shard.queue" \
            --routing-key "factory.telemetry.electricity.shard-$shard.#" \
            --idempotently || true

        rabbitmqadmin -q --non-interactive bindings delete \
            --source iot.telemetry.dlx \
            --destination-type queue \
            --destination "telemetry.electricity.shard-$shard.dlq" \
            --routing-key "telemetry.electricity.shard-$shard.dead" \
            --idempotently || true

        rabbitmqadmin -q --non-interactive queues delete \
            --name "telemetry.electricity.shard-$shard.queue" \
            --idempotently || true

        rabbitmqadmin -q --non-interactive queues delete \
            --name "telemetry.electricity.shard-$shard.dlq" \
            --idempotently || true
    done
}

while [ "$attempt" -le "$max_attempts" ]; do
    nodes="$(rabbitmqadmin -q --non-interactive nodes list 2>/tmp/rabbitmqadmin-nodes.err || true)"
    node_count="$(printf "%s\n" "$nodes" | grep -c 'rabbit@' || true)"

    if [ "$node_count" -ge "$expected_nodes" ]; then
        echo "RabbitMQ cluster has $node_count node(s); importing topology"
        cleanup_obsolete_electricity_topology
        rabbitmqadmin --non-interactive definitions import --file "$definitions_file"
        echo "Imported RabbitMQ topology from $definitions_file"
        exit 0
    fi

    echo "Waiting for RabbitMQ cluster: $node_count/$expected_nodes node(s) visible (attempt $attempt/$max_attempts)"
    attempt=$((attempt + 1))
    sleep 2
done

echo "RabbitMQ cluster did not reach $expected_nodes node(s) before timeout" >&2
cat /tmp/rabbitmqadmin-nodes.err >&2 || true
exit 1
