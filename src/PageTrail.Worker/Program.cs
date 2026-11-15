using PageTrail.Infrastructure;
using PageTrail.Worker.Fetching;
using PageTrail.Worker.Health;
using PageTrail.Worker.Processing;
using PageTrail.Worker.Robots;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddSingleton<BrokerConnectionState>();
builder.Services.AddHealthChecks().AddCheck<BrokerConnectionHealthCheck>("rabbitmq");
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<HtmlPageFetcher>(http => http.DefaultRequestHeaders.UserAgent.ParseAdd($"{RobotsRules.UserAgentToken}/1.0"))
    .ConfigurePrimaryHttpMessageHandler(PrivateAddressGuard.CreateHandler)
    .AddStandardResilienceHandler();
builder.Services.AddScoped<RobotsPolicy>();
builder.Services.AddScoped<CrawlPageProcessor>();
builder.Services.AddScoped<CrawlDeliveryHandler>();
builder.Services.AddHostedService<CrawlPageConsumer>();

var app = builder.Build();

app.MapHealthChecks("/health");

await app.RunAsync();
