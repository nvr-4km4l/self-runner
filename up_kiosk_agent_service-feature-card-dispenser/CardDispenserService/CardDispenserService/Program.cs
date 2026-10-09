using CardDispenserAgent.Configuration;
using CardDispenserAgent.Hubs;
using CardDispenserAgent.Serial;
using CardDispenserAgent.Services;
//using CardDispenserService.Services;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;


var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

// Configuration

builder.Services.Configure<DispenserOptions>(builder.Configuration.GetSection("Dispenser"));


// Register Options object

builder.Services.AddSingleton(sp =>
{
    return sp.GetRequiredService<IOptions<DispenserOptions>>().Value;
});

// Controllers
builder.Services.AddControllers();

// SignalR
//builder.Services.AddSignalR();


// Swagger
//builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});

// Dependency Injection
builder.Services.AddSingleton<SerialPortManager>();
builder.Services.AddSingleton<CardReaderService>();
builder.Services.AddSingleton<CardDispenserService>();
builder.Services.AddSingleton<VmsSignalRClient>();
builder.Services.AddSingleton<DeviceIdentityStore>();
builder.Services.AddSingleton<DeviceIdentityService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
//app.MapHub<DispenserHub>("/dispenserHub");



// Start Serial Connection
var serialPortManager = app.Services.GetRequiredService<SerialPortManager>();

try
{
    serialPortManager.StartConnectionLoop();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    logger.LogError(ex,"Failed to start dispenser serial connection");
}

var vmsSignalRClient = app.Services.GetRequiredService<VmsSignalRClient>();

try
{
    await vmsSignalRClient.StartAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    logger.LogError(
        ex,
        "Failed to connect to VMS SignalR");
}

app.Lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        serialPortManager
            .StopConnectionLoopAsync()
            .GetAwaiter()
            .GetResult();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        logger.LogError(ex, "Failed to stop dispenser serial connection");
    }
});

app.Run();