using Hgerman.ContentStudio.Infrastructure.DependencyInjection;
using Hgerman.ContentStudio.Shared.Options;
using Hgerman.ContentStudio.Worker;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Infrastructure.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(
builder.Configuration.GetSection("Worker"));

builder.Services.AddContentStudioInfrastructure(builder.Configuration);
builder.Services.AddHostedService<WorkerService>();

builder.Services.AddScoped<IAutomationService, AutomationService>();
builder.Services.AddScoped<ITitleOptimizationService, TitleOptimizationService>();

var host = builder.Build();
host.Run();