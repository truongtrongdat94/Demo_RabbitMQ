# Local TLS Demo Certificates

This folder contains tooling for local RabbitMQ TLS demo certificates.

Generate certificates:

```powershell
.\infra\tls\generate-local-certs.ps1
```

The script writes files to `infra/tls/generated/`:

```text
ca.crt
ca.key
server.crt
server.key
```

`generated/` is ignored by git because it contains private keys. The server certificate includes SAN entries for:

```text
rabbitmq-lb
rabbitmq-1
rabbitmq-2
rabbitmq-3
localhost
127.0.0.1
```

Run the TLS demo stack:

```powershell
docker compose -f docker-compose.yml -f docker-compose.tls.yml up -d --build
```

AMQPS is exposed on `localhost:5671`. MQTTS is exposed on `localhost:18884`.
