FROM rabbitmq:4.3-management

COPY enabled_plugins /etc/rabbitmq/enabled_plugins
COPY definitions.template.json /work/definitions.template.json
COPY rabbitmq.conf.template /work/rabbitmq.conf.template
COPY scripts/render-definitions.sh /work/render-definitions.sh

RUN chmod 0644 /etc/rabbitmq/enabled_plugins \
    && chmod 0644 /work/definitions.template.json /work/rabbitmq.conf.template \
    && chmod 0755 /work/render-definitions.sh
