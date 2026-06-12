using System.Diagnostics;
using Npgsql;

namespace PRO.Infrastructure.Services;

public static class PostgresBackupUtility
{
    public static async Task<PostgresBackupResult> CreateBackupAsync(
        string connectionString,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolPath = FindTool("pg_dump");
            if (string.IsNullOrWhiteSpace(toolPath))
                return PostgresBackupResult.Fail("未找到 pg_dump，请安装 PostgreSQL Client Tools 并加入 PATH");

            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Host)
                || string.IsNullOrWhiteSpace(builder.Database)
                || string.IsNullOrWhiteSpace(builder.Username))
            {
                return PostgresBackupResult.Fail("PostgreSQL 连接字符串不完整，无法执行备份");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            if (File.Exists(filePath))
                File.Delete(filePath);

            var result = await RunToolAsync(
                toolPath,
                new[]
                {
                    "--format=custom",
                    "--blobs",
                    "--no-owner",
                    "--no-privileges",
                    "--file", filePath,
                    "--host", builder.Host,
                    "--port", builder.Port.ToString(),
                    "--username", builder.Username,
                    builder.Database
                },
                builder.Password,
                cancellationToken);

            if (result.ExitCode != 0 || !File.Exists(filePath))
                return PostgresBackupResult.Fail(TrimToolMessage(result.Error, result.Output, "pg_dump 执行失败"));

            var fileInfo = new FileInfo(filePath);
            return PostgresBackupResult.Ok(fileInfo.Length, $"备份成功，工具：{toolPath}", toolPath);
        }
        catch (Exception ex)
        {
            return PostgresBackupResult.Fail($"备份失败: {ex.Message}");
        }
    }

    public static async Task<PostgresBackupResult> RestoreBackupAsync(
        string connectionString,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return PostgresBackupResult.Fail("备份文件不存在");

            var toolPath = FindTool("pg_restore");
            if (string.IsNullOrWhiteSpace(toolPath))
                return PostgresBackupResult.Fail("未找到 pg_restore，请安装 PostgreSQL Client Tools 并加入 PATH");

            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Host)
                || string.IsNullOrWhiteSpace(builder.Database)
                || string.IsNullOrWhiteSpace(builder.Username))
            {
                return PostgresBackupResult.Fail("PostgreSQL 连接字符串不完整，无法执行恢复");
            }

            var result = await RunToolAsync(
                toolPath,
                new[]
                {
                    "--clean",
                    "--if-exists",
                    "--no-owner",
                    "--no-privileges",
                    "--dbname", builder.Database,
                    "--host", builder.Host,
                    "--port", builder.Port.ToString(),
                    "--username", builder.Username,
                    filePath
                },
                builder.Password,
                cancellationToken);

            return result.ExitCode == 0
                ? PostgresBackupResult.Ok(new FileInfo(filePath).Length, $"恢复成功，工具：{toolPath}", toolPath)
                : PostgresBackupResult.Fail(TrimToolMessage(result.Error, result.Output, "pg_restore 执行失败"));
        }
        catch (Exception ex)
        {
            return PostgresBackupResult.Fail($"恢复失败: {ex.Message}");
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunToolAsync(
        string toolPath,
        IEnumerable<string> args,
        string? password,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        if (!string.IsNullOrEmpty(password))
            startInfo.Environment["PGPASSWORD"] = password;

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static string? FindTool(string toolName)
    {
        var executable = toolName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? toolName
            : $"{toolName}.exe";

        var explicitPath = Environment.GetEnvironmentVariable($"{toolName.ToUpperInvariant()}_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var path in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(path.Trim(), executable);
            if (File.Exists(candidate))
                return candidate;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var postgresRoot = Path.Combine(programFiles, "PostgreSQL");
        if (Directory.Exists(postgresRoot))
        {
            var candidate = Directory.GetDirectories(postgresRoot)
                .Select(d => Path.Combine(d, "bin", executable))
                .Where(File.Exists)
                .OrderByDescending(p => p)
                .FirstOrDefault();
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    private static string TrimToolMessage(string error, string output, string fallback)
    {
        var message = !string.IsNullOrWhiteSpace(error) ? error : output;
        if (string.IsNullOrWhiteSpace(message))
            return fallback;

        message = message.Trim();
        return message.Length <= 500 ? message : message[..500];
    }
}

public sealed record PostgresBackupResult(bool Success, string Message, long FileSize = 0, string? ToolPath = null)
{
    public static PostgresBackupResult Ok(long fileSize, string message, string? toolPath = null)
        => new(true, message, fileSize, toolPath);

    public static PostgresBackupResult Fail(string message)
        => new(false, message);
}
