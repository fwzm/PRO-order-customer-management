using Microsoft.Extensions.Configuration;

namespace PRO.Infrastructure.Configuration;

public static class HardcodedConfig
{
    private static IConfigurationRoot? _configuration;

    public static void Initialize(string basePath)
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();
    }

    private static string? GetValue(string section, string key)
    {
        var envValue = Environment.GetEnvironmentVariable($"PRO_{section}__{key}")
            ?? Environment.GetEnvironmentVariable($"{section}__{key}");

        return !string.IsNullOrWhiteSpace(envValue)
            ? envValue
            : _configuration?[$"{section}:{key}"];
    }

    private static string? GetConnectionString(string name)
    {
        var envValue = Environment.GetEnvironmentVariable($"PRO_ConnectionStrings__{name}")
            ?? Environment.GetEnvironmentVariable($"ConnectionStrings__{name}")
            ?? Environment.GetEnvironmentVariable($"PRO_{name}_CONNECTION_STRING");

        return !string.IsNullOrWhiteSpace(envValue)
            ? envValue
            : _configuration?.GetConnectionString(name);
    }

    // ========== PostgreSQL 数据库连接 ==========
    /// <summary>PostgreSQL 连接字符串</summary>
    public static string PostgreSQLConnectionString =>
        GetConnectionString("PostgreSQL") ?? string.Empty;

    /// <summary>是否已配置 PostgreSQL 连接</summary>
    public static bool HasPostgreSQLConfig =>
        !string.IsNullOrEmpty(PostgreSQLConnectionString);

    // ========== 企业微信配置 ==========
    public static string WeChatCorpId => GetValue("WeChat", "CorpId") ?? string.Empty;
    public static string WeChatCorpSecret => GetValue("WeChat", "CorpSecret") ?? string.Empty;
    public static string WeChatAgentId => GetValue("WeChat", "AgentId") ?? string.Empty;
    public static bool WeChatEnabled => bool.TryParse(GetValue("WeChat", "Enabled"), out var enabled) && enabled;

    public static bool HasWeChatConfig => !string.IsNullOrEmpty(WeChatCorpId) && !string.IsNullOrEmpty(WeChatCorpSecret);

    // ========== 腾讯地图配置 ==========
    /// <summary>腾讯地图 WebService API Key</summary>
    public static string TencentMapApiKey =>
        GetValue("TencentMap", "ApiKey") ?? string.Empty;

    /// <summary>是否已配置腾讯地图</summary>
    public static bool HasTencentMapConfig =>
        !string.IsNullOrEmpty(TencentMapApiKey);
}
