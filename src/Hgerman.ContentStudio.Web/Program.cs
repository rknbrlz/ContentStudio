using Hgerman.ContentStudio.Infrastructure.DependencyInjection;
using Hgerman.ContentStudio.Shared.Options;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.AddContentStudioInfrastructure(builder.Configuration);
builder.Services.AddScoped<IAutomationService, AutomationService>();
builder.Services.AddScoped<ITitleOptimizationService, TitleOptimizationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();