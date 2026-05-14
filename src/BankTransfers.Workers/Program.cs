using Microsoft.Extensions.Hosting;
using BankTransfers.Workers;
using BankTransfers.Infrastructure.Persistence;
using BankTransfers.Application.Abstractions;
using BankTransfers.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrWhiteSpace(postgresConnectionString))
{
    builder.Services.AddDbContext<SavranPayDbContext>(options => options.UseNpgsql(postgresConnectionString));
}

builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddSingleton<INotificationSender, LoggingNotificationSender>();

await builder.Build().RunAsync();
