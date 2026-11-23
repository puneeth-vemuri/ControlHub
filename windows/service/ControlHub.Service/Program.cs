using ControlHub.Service.Core;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ControlHub Service";
});
builder.Logging.AddProvider(new FileLoggerProvider());
builder.Services.AddSingleton<CompanionState>();
builder.Services.AddSingleton<IPowerExecutor, WindowsPowerExecutor>();
builder.Services.AddHostedService<ServicePipeWorker>();
builder.Services.AddHostedService<CompanionPipeWorker>();

var host = builder.Build();
host.Run();
