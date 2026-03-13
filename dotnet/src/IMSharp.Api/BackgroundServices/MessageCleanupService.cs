using System.Diagnostics;
using IMSharp.Api.Configuration;
using IMSharp.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace IMSharp.Api.BackgroundServices;

public class MessageCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageCleanupService> _logger;
    private readonly MessageCleanupOptions _options;

    public MessageCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<MessageCleanupService> logger,
        IOptions<MessageCleanupOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("消息自动清理功能已禁用");
            return;
        }

        _logger.LogInformation("消息自动清理服务已启动,保留天数: {RetentionDays}, 执行时间: {ScheduleTime}",
            _options.RetentionDays, _options.ScheduleTime);

        var now = DateTimeOffset.Now;
        var scheduleTime = TimeOnly.Parse(_options.ScheduleTime);
        var nextRun = CalculateNextRunTime(now, scheduleTime);

        _logger.LogInformation("下次清理时间: {NextRun}", nextRun);

        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));

        // 首次延迟到指定时间
        var delay = nextRun - now;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, stoppingToken);
        }

        // 循环执行清理
        do
        {
            await CleanupMessagesAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private DateTimeOffset CalculateNextRunTime(DateTimeOffset now, TimeOnly scheduleTime)
    {
        var today = DateOnly.FromDateTime(now.DateTime);
        var scheduledDateTime = today.ToDateTime(scheduleTime);
        var scheduledOffset = new DateTimeOffset(scheduledDateTime, now.Offset);

        if (scheduledOffset <= now)
        {
            // 如果今天的时间已过,则安排到明天
            scheduledOffset = scheduledOffset.AddDays(1);
        }

        return scheduledOffset;
    }

    private async Task CleanupMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var privateRepo = scope.ServiceProvider.GetRequiredService<IPrivateMessageRepository>();
        var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();

        var cutoffTime = DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("消息清理任务开始执行,保留天数: {RetentionDays}, 截止时间: {CutoffTime}",
                _options.RetentionDays, cutoffTime);

            var privateCount = await privateRepo.DeleteOldMessagesAsync(cutoffTime, cancellationToken);
            var groupCount = await groupRepo.DeleteOldGroupMessagesAsync(cutoffTime, cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation("消息清理完成 - 私聊消息: {PrivateCount} 条, 群聊消息: {GroupCount} 条, 耗时: {Elapsed}ms",
                privateCount, groupCount, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "消息清理任务执行失败,将在下次调度时重试");
        }
    }
}
