#!/bin/sh
set -eu

template_path="/work/definitions.template.json"
config_template_path="/work/rabbitmq.conf.template"
output_path="/generated/definitions.json"
config_output_path="/generated/rabbitmq.conf"

require_env() {
    name="$1"
    eval "value=\${$name:-}"
    if [ -z "$value" ]; then
        echo "$name is required" >&2
        exit 1
    fi
}

validate_identifier() {
    name="$1"
    value="$2"
    case "$value" in
        *[!A-Za-z0-9_.-]*)
            echo "$name may only contain letters, numbers, underscore, dot, or dash" >&2
            exit 1
            ;;
    esac
}

for name in \
    RABBITMQ_VHOST \
    RABBITMQ_ADMIN_USER \
    RABBITMQ_ADMIN_PASSWORD \
    RABBITMQ_PUBLISHER_USER \
    RABBITMQ_PUBLISHER_PASSWORD \
    RABBITMQ_CONSUMER_USER \
    RABBITMQ_CONSUMER_PASSWORD
do
    require_env "$name"
done

validate_identifier RABBITMQ_VHOST "$RABBITMQ_VHOST"
validate_identifier RABBITMQ_ADMIN_USER "$RABBITMQ_ADMIN_USER"
validate_identifier RABBITMQ_PUBLISHER_USER "$RABBITMQ_PUBLISHER_USER"
validate_identifier RABBITMQ_CONSUMER_USER "$RABBITMQ_CONSUMER_USER"

hash_password() {
    password="$1"
    temp_file="$(mktemp)"
    rabbitmqctl hash_password "$password" > "$temp_file"
    tail -n 1 "$temp_file" | tr -d '\r'
    rm -f "$temp_file"
}

admin_password_hash="$(hash_password "$RABBITMQ_ADMIN_PASSWORD")"
publisher_password_hash="$(hash_password "$RABBITMQ_PUBLISHER_PASSWORD")"
consumer_password_hash="$(hash_password "$RABBITMQ_CONSUMER_PASSWORD")"

mkdir -p "$(dirname "$output_path")"
sed \
    -e "s@__RABBITMQ_VHOST__@$RABBITMQ_VHOST@g" \
    -e "s@__RABBITMQ_ADMIN_USER__@$RABBITMQ_ADMIN_USER@g" \
    -e "s@__RABBITMQ_ADMIN_PASSWORD_HASH__@$admin_password_hash@g" \
    -e "s@__RABBITMQ_PUBLISHER_USER__@$RABBITMQ_PUBLISHER_USER@g" \
    -e "s@__RABBITMQ_PUBLISHER_PASSWORD_HASH__@$publisher_password_hash@g" \
    -e "s@__RABBITMQ_CONSUMER_USER__@$RABBITMQ_CONSUMER_USER@g" \
    -e "s@__RABBITMQ_CONSUMER_PASSWORD_HASH__@$consumer_password_hash@g" \
    "$template_path" > "$output_path"

chmod 644 "$output_path"
echo "Rendered RabbitMQ definitions to $output_path"

sed \
    -e "s@__RABBITMQ_VHOST__@$RABBITMQ_VHOST@g" \
    "$config_template_path" > "$config_output_path"

chmod 644 "$config_output_path"
echo "Rendered RabbitMQ config to $config_output_path"
