using ConsumerService.Messaging;
using ConsumerService.Options;
using ConsumerService.Processing;
using ConsumerService.Storage;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .Validate(RabbitMqOptions.Validate, "RabbitMQ configuration is invalid")
    .ValidateOnStart();

builder.Services
    .AddOptions<MongoOptions>()
    .Bind(builder.Configuration.GetSection(MongoOptions.SectionName))
    .Validate(MongoOptions.Validate, "MongoDB configuration is invalid")
    .ValidateOnStart();

builder.Services.AddRepositories();
builder.Services.AddSingleton<TelemetryMessageProcessor>();
builder.Services.AddHostedService<RabbitMqConsumerWorker>();

await builder.Build().RunAsync();
