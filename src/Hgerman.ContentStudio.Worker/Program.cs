using Hgerman.ContentStudio.Infrastructure.DependencyInjection;
using Hgerman.ContentStudio.Shared.Options;
using Hgerman.ContentStudio.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection("Worker"));

builder.Services.AddContentStudioInfrastructure(builder.Configuration);
builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
host.Run();