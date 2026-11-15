using PageTrail.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
