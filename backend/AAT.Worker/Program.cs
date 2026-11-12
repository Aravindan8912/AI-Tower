using AAT.Infrastructure;
using AAT.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAatInfrastructure(builder.Configuration);
builder.Services.AddHostedService<TelemetryRetentionService>();

var host = builder.Build();
await host.RunAsync();
