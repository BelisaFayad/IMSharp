namespace IMSharp.Api.Configuration;

/// <summary>
/// 数据库自动迁移配置选项
/// </summary>
public class DatabaseMigrationOptions
{
    /// <summary>
    /// 是否在启动时自动执行数据库迁移
    /// </summary>
    public bool AutoMigrateOnStartup { get; set; } = true;

    /// <summary>
    /// 迁移失败时是否停止应用程序
    /// </summary>
    public bool StopApplicationOnFailure { get; set; } = true;

    /// <summary>
    /// 迁移超时时间(秒),0 表示使用默认值
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}
