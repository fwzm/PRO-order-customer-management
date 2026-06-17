namespace PRO.Mobile;

/// <summary>
/// 应用全局配置 — 通过环境变量或编译时常量注入
/// </summary>
public static class AppConfig
{
    /// <summary>
    /// WebApi 基地址 — 开发环境默认 localhost:5000，生产通过环境变量 PRO_API_BASE_URL 覆盖
    /// </summary>
#if DEBUG
    public static string ApiBaseUrl => "http://10.0.2.2:5000"; // Android 模拟器 → 宿主机
#else
    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("PRO_API_BASE_URL") ?? "http://localhost:5000";
#endif
}
