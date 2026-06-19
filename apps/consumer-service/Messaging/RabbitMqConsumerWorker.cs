using ConsumerService.Options;
using ConsumerService.Processing;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConsumerService.Messaging;

public sealed class RabbitMqConsumerWorker : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly TelemetryMessageProcessor _processor;
    private readonly ILogger<RabbitMqConsumerWorker> _logger;

    public RabbitMqConsumerWorker(
        IOptions<RabbitMqOptions> options,
        TelemetryMessageProcessor processor,
        ILogger<RabbitMqConsumerWorker> logger)
    {
        _options = options.Value;
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.UserName,
            Password = _options.Password,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ConsumerDispatchConcurrency = 1
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        var consumers = new List<QueueConsumer>();

        foreach (var queueName in _options.Queues)
        {
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: _options.PrefetchCount,
                global: false,
                cancellationToken: stoppingToken);

            var consumer = new QueueConsumer(
                queueName,
                channel,
                _processor,
                _logger);

            await consumer.StartAsync(stoppingToken);
            consumers.Add(consumer);
        }

        _logger.LogInformation(
            "Consumer service is listening to {QueueCount} queues: {Queues}",
            consumers.Count,
            string.Join(", ", consumers.Select(consumer => consumer.QueueName)));

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Consumer service is stopping");
        }
        finally
        {
            foreach (var consumer in consumers)
            {
                await consumer.DisposeAsync();
            }
        }
    }

    private sealed class QueueConsumer : IAsyncDisposable
    {
        private readonly IChannel _channel;
        private readonly TelemetryMessageProcessor _processor;
        private readonly ILogger _logger;

        public QueueConsumer(
            string queueName,
            IChannel channel,
            TelemetryMessageProcessor processor,
            ILogger logger)
        {
            QueueName = queueName;
            _channel = channel;
            _processor = processor;
            _logger = logger;
        }

        public string QueueName { get; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += OnReceivedAsync;

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }

        private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
        {
            try
            {
                await _processor.ProcessAsync(
                    args.Body,
                    QueueName,
                    args.RoutingKey,
                    CancellationToken.None);

                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
            }
            catch (PermanentMessageException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Rejecting permanent failure from queue {QueueName}; routing key {RoutingKey}; delivery tag {DeliveryTag}",
                    QueueName,
                    args.RoutingKey,
                    args.DeliveryTag);

                await _channel.BasicRejectAsync(args.DeliveryTag, requeue: false);
            }
            catch (TransientMessageException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Rejecting transient failure from queue {QueueName}; routing key {RoutingKey}; delivery tag {DeliveryTag}; requeue=true",
                    QueueName,
                    args.RoutingKey,
                    args.DeliveryTag);

                await _channel.BasicRejectAsync(args.DeliveryTag, requeue: true);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Rejecting unexpected consumer failure from queue {QueueName}; routing key {RoutingKey}; delivery tag {DeliveryTag}; requeue=true",
                    QueueName,
                    args.RoutingKey,
                    args.DeliveryTag);

                await _channel.BasicRejectAsync(args.DeliveryTag, requeue: true);
            }
        }
    }
}
