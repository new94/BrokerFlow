using BrokerFlow.Api.Hubs;
using BrokerFlow.Api.Models;
using BrokerFlow.Api.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// ── Database ─────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=BrokerFlow;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<BrokerFlowDbContext>(opts =>
    opts.UseSqlServer(connectionString));

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<FileParserService>();
builder.Services.AddSingleton<MappingEngineService>();
builder.Services.AddScoped<JobProcessingService>();
builder.Services.AddScoped<SchedulerService>();

// ── Quartz Scheduler ─────────────────────────────────────────────────────────
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
});
builder.Services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Controllers + Swagger ────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddNewtonsoftJson(opts =>
    {
        opts.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        opts.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Include;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "BrokerFlow API", Version = "v1" });
});

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
    opts.AddPolicy("SignalR", policy =>
    {
        policy.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

var app = builder.Build();

// ── Database migration ───────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BrokerFlowDbContext>();
    db.Database.Migrate();

    // Seed default config
    if (!db.AppConfigs.Any())
    {
        var basePath = builder.Configuration["Paths:Base"] ?? AppContext.BaseDirectory;
        db.AppConfigs.AddRange(
            new AppConfig { Key = "reports_dir", Value = builder.Configuration["Paths:Reports"] ?? Path.Combine(basePath, "reports") },
            new AppConfig { Key = "output_dir", Value = builder.Configuration["Paths:Output"] ?? Path.Combine(basePath, "output") },
            new AppConfig { Key = "uploads_dir", Value = builder.Configuration["Paths:Uploads"] ?? Path.Combine(basePath, "uploads") }
        );
        db.SaveChanges();
    }

    // Create directories
    foreach (var config in db.AppConfigs.Where(c => c.Key.EndsWith("_dir")).ToList())
    {
        try { Directory.CreateDirectory(config.Value); } catch { }
    }
}

// ── Sync schedules ───────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var scheduler = scope.ServiceProvider.GetRequiredService<SchedulerService>();
    await scheduler.SyncSchedulesAsync();
}

// ── Middleware pipeline ──────────────────────────────────────────────────────
// Swagger enabled in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

// Serve static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();
app.MapHub<JobHub>("/hubs/jobs").RequireCors("SignalR");

// SPA fallback — any non-API, non-file request serves index.html
app.MapFallbackToFile("index.html");

app.Run();
