using PageTrail.Infrastructure;
using PageTrail.Worker.Fetching;
using PageTrail.Worker.Health;
using PageTrail.Worker.Processing;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddSingleton<BrokerConnectionState>();
builder.Services.AddHealthChecks().AddCheck<BrokerConnectionHealthCheck>("rabbitmq");
builder.Services.AddHttpClient<HtmlPageFetcher>().AddStandardResilienceHandler();
builder.Services.AddScoped<CrawlPageProcessor>();
builder.Services.AddScoped<CrawlDeliveryHandler>();
builder.Services.AddHostedService<CrawlPageConsumer>();

var app = builder.Build();

app.MapHealthChecks("/health");

await app.RunAsync();
