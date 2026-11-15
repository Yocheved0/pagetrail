using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PageTrail.Api.Dispatching;
using PageTrail.Api.Jobs;
using PageTrail.Infrastructure;
using PageTrail.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOptions<CrawlLimitsOptions>()
    .Bind(builder.Configuration.GetSection(CrawlLimitsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IPagePublisher, RabbitMqPagePublisher>();
builder.Services.AddHostedService<PageDispatcher>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<PageTrailDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapHealthChecks("/health");
app.MapJobEndpoints();

await app.RunAsync();

public partial class Program;
