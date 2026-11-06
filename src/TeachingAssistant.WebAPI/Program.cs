using Serilog;
using TeachingAssistant.Infrastructure.Persistence;
using TeachingAssistant.WebAPI.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServices(builder.Configuration);

var app = builder.Build();

// In development the local database is brought up to date on start, so a new migration never surfaces as a missing
// table. Elsewhere (a shared or production database) migrating stays a deliberate step.
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync();
}

// Exceptions become ProblemDetails responses (see ApplicationExceptionHandler).
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// One log line per request (method, path, status, duration).
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
