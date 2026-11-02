using Serilog;
using TeachingAssistant.WebAPI.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServices(builder.Configuration);

var app = builder.Build();

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
