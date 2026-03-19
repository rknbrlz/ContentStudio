using Hgerman.ContentStudio.Infrastructure.DependencyInjection;
using Hgerman.ContentStudio.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddContentStudioInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();