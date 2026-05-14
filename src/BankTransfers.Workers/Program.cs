using Microsoft.Extensions.Hosting;
using BankTransfers.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<OutboxWorker>();

await builder.Build().RunAsync();
