using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using TeachingAssistant.Infrastructure.Persistence;
using TeachingAssistant.WebAPI.Extensions;

const string MigrateSwitch = "--migrate";

var builder = WebApplication.CreateBuilder(args.Where(arg => arg != MigrateSwitch).ToArray());

builder.Services.AddServices(builder.Configuration);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (args.Contains(MigrateSwitch))
{
    await app.Services.MigrateDatabaseAsync();
    return;
}

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync();
}

app.UseForwardedHeaders();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
