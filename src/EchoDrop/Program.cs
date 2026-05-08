using EchoDrop;
using EchoDrop.Domain.Interfaces;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .ConfigureEchoDropOptions(builder.Configuration)
    .AddEchoDropServices(builder.Configuration);

var host = builder.Build();

await host.Services.GetRequiredService<IScheduledPostRepository>().EnsureSchemaAsync(CancellationToken.None).ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);
