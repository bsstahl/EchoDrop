using EchoDrop.Api;
using EchoDrop;
using EchoDrop.Domain.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .ConfigureEchoDropOptions(builder.Configuration)
    .AddEchoDropServices();

var app = builder.Build();

await app.Services.GetRequiredService<IScheduledPostRepository>().EnsureSchemaAsync(CancellationToken.None).ConfigureAwait(false);

app.MapDataUpdateApi();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
