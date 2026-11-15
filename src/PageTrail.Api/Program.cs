using Microsoft.EntityFrameworkCore;
using PageTrail.Infrastructure;
using PageTrail.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<PageTrailDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapHealthChecks("/health");

await app.RunAsync();

public partial class Program;
