using Microsoft.Extensions.DependencyInjection;
using OMS.Api;
using OMS.Worker.Models;
using OMS.Worker.Services;
using Temporalio.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<InMemoryOrderRepository>();

builder.Services.AddSingleton<ITemporalClient>(sp =>
{
    return TemporalClient.ConnectAsync(new()
    {
        TargetHost = "localhost:7233",
        Namespace = TemporalConstants.Namespace
    }).GetAwaiter().GetResult();
});

builder.Services.AddHostedService<TemporalWorkerHostedService>();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "ok",
        temporal = "localhost:7233"
    }));

app.Run();
