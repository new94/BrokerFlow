using BrokerFlow.Api.Models;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace BrokerFlow.Api.Services;

[DisallowConcurrentExecution]
public class ScheduledScanJob : IJob
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ScheduledScanJob> _logger;

    public ScheduledScanJob(IServiceProvider services, ILogger<ScheduledScanJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var scheduleId = context.MergedJobDataMap.GetString("ScheduleId");
        if (string.IsNullOrEmpty(scheduleId)) return;

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrokerFlowDbContext>();
        var jobProcessor = scope.ServiceProvider.GetRequiredService<JobProcessingService>();

        var schedule = await db.Schedules.FindAsync(scheduleId);
        if (schedule == null || !schedule.Enabled) return;

        var source = await db.Sources.FindAsync(schedule.SourceId);
        if (source == null || !source.Enabled) return;

        _logger.LogInformation("Running scheduled scan for source {SourceId}", source.Id);

        try
        {
            // Scan directory
            var dir = source.Path;
            if (!Directory.Exists(dir))
            {
                _logger.LogWarning("Source directory does not exist: {Dir}", dir);
                return;
            }

            var mask = source.FileMask ?? "*.*";
            var files = Directory.GetFiles(dir, mask, SearchOption.TopDirectoryOnly);

            foreach (var filePath in files)
            {
                // Check if already processed
                var alreadyProcessed = await db.ProcessingJobs
                    .AnyAsync(j => j.FilePath == filePath && j.Status == "done");
                if (alreadyProcessed) continue;

                // Create job
                var job = new ProcessingJob
                {
                    SourceId = source.Id,
                    MappingId = schedule.MappingId,
                    FilePath = filePath,
                    OriginalFileName = Path.GetFileName(filePath),
                    Status = "pending"
                };
                db.ProcessingJobs.Add(job);
                await db.SaveChangesAsync();

                // Process
                await jobProcessor.ProcessJobAsync(job.Id);
            }

            schedule.LastRunAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled scan failed for schedule {ScheduleId}", scheduleId);
        }
    }
}

public class SchedulerService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IServiceProvider _services;

    public SchedulerService(ISchedulerFactory schedulerFactory, IServiceProvider services)
    {
        _schedulerFactory = schedulerFactory;
        _services = services;
    }

    public async Task SyncSchedulesAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrokerFlowDbContext>();
        var scheduler = await _schedulerFactory.GetScheduler();

        var schedules = await db.Schedules.Where(s => s.Enabled).ToListAsync();

        // Clear existing jobs
        var existingKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals("brokerflow"));
        foreach (var key in existingKeys)
        {
            await scheduler.DeleteJob(key);
        }

        foreach (var schedule in schedules)
        {
            try
            {
                var jobKey = new JobKey($"schedule_{schedule.Id}", "brokerflow");
                var job = JobBuilder.Create<ScheduledScanJob>()
                    .WithIdentity(jobKey)
                    .UsingJobData("ScheduleId", schedule.Id)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"trigger_{schedule.Id}", "brokerflow")
                    .WithCronSchedule(schedule.CronExpression)
                    .Build();

                await scheduler.ScheduleJob(job, trigger);

                // Update next run time
                var nextFire = trigger.GetNextFireTimeUtc();
                schedule.NextRunAt = nextFire?.UtcDateTime;
            }
            catch (Exception)
            {
                // Invalid cron expression, skip
            }
        }

        await db.SaveChangesAsync();
    }
}
