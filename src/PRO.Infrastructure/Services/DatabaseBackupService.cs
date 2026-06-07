using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRO.Domain.Entities;
using PRO.Infrastructure.Configuration;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// PostgreSQL 数据库备份服务 - 使用 pg_dump 实际备份
/// </summary>
public class DatabaseBackupService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _backupDir;

    public DatabaseBackupService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PRO", "backups");
        Directory.CreateDirectory(_backupDir);
    }

    /// <summary>
    /// 执行自动备份
    /// </summary>
    public async Task<BackupResult> PerformAutoBackupAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

            // 检查是否启用了自动备份
            var settings = await dbContext.LocalSettings.ToListAsync();
            var closeSetting = settings.FirstOrDefault(s => s.SettingKey == "CloseBehavior");
            if (closeSetting != null && int.Parse(closeSetting.SettingValue) == 1)
                return new BackupResult { Success = false, Message = "自动备份已禁用" };

            // 清理过期备份记录
            await CleanupExpiredBackupsAsync(dbContext);

            // 执行实际备份
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"pro_backup_{timestamp}.dump";
            var filePath = Path.Combine(_backupDir, fileName);

            var backupResult = await PostgresBackupUtility.CreateBackupAsync(
                HardcodedConfig.PostgreSQLConnectionString,
                filePath);

            if (backupResult.Success)
            {
                var fileInfo = new FileInfo(filePath);
                var record = new BackupRecord
                {
                    FileName = fileName,
                    FilePath = filePath,
                    BackupType = "自动",
                    FileSize = fileInfo.Length,
                    BackupTime = DateTime.Now,
                    ExpireTime = DateTime.Now.AddDays(30),
                    Status = "Success",
                    Remark = backupResult.Message
                };

                dbContext.BackupRecords.Add(record);
                await dbContext.SaveChangesAsync();

                Log.Information("自动备份成功: {FileName}, 大小: {Size}KB", fileName, fileInfo.Length / 1024);
                return new BackupResult { Success = true, Message = "备份成功", FileName = fileName, FileSize = fileInfo.Length };
            }
            else
            {
                // 记录失败
                dbContext.BackupRecords.Add(new BackupRecord
                {
                    FileName = fileName,
                    FilePath = filePath,
                    BackupType = "自动",
                    FileSize = 0,
                    BackupTime = DateTime.Now,
                    ExpireTime = DateTime.Now.AddDays(7),
                    Status = "Failed",
                    Remark = backupResult.Message
                });
                await dbContext.SaveChangesAsync();

                return new BackupResult { Success = false, Message = backupResult.Message };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "自动备份异常");
            return new BackupResult { Success = false, Message = $"备份异常: {ex.Message}" };
        }
    }

    /// <summary>
    /// 执行手动备份
    /// </summary>
    public async Task<BackupResult> PerformManualBackupAsync(string? remark = null)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"pro_backup_manual_{timestamp}.dump";
            var filePath = Path.Combine(_backupDir, fileName);

            var backupResult = await PostgresBackupUtility.CreateBackupAsync(
                HardcodedConfig.PostgreSQLConnectionString,
                filePath);

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

            if (backupResult.Success)
            {
                var fileInfo = new FileInfo(filePath);
                var record = new BackupRecord
                {
                    FileName = fileName,
                    FilePath = filePath,
                    BackupType = "手动",
                    FileSize = fileInfo.Length,
                    BackupTime = DateTime.Now,
                    ExpireTime = DateTime.Now.AddDays(90),
                    Status = "Success",
                    Remark = string.IsNullOrWhiteSpace(remark) ? backupResult.Message : $"{remark}; {backupResult.Message}"
                };

                dbContext.BackupRecords.Add(record);
                await dbContext.SaveChangesAsync();

                Log.Information("手动备份成功: {FileName}", fileName);
                return new BackupResult { Success = true, Message = "备份成功", FileName = fileName, FileSize = fileInfo.Length };
            }
            else
            {
                dbContext.BackupRecords.Add(new BackupRecord
                {
                    FileName = fileName,
                    FilePath = filePath,
                    BackupType = "手动",
                    FileSize = 0,
                    BackupTime = DateTime.Now,
                    ExpireTime = DateTime.Now.AddDays(30),
                    Status = "Failed",
                    Remark = string.IsNullOrWhiteSpace(remark) ? backupResult.Message : $"{remark}; {backupResult.Message}"
                });
                await dbContext.SaveChangesAsync();

                return new BackupResult { Success = false, Message = backupResult.Message };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "手动备份异常");
            return new BackupResult { Success = false, Message = $"备份异常: {ex.Message}" };
        }
    }

    /// <summary>
    /// 恢复数据库
    /// </summary>
    public async Task<BackupResult> RestoreDatabaseAsync(string backupFilePath)
    {
        try
        {
            if (!File.Exists(backupFilePath))
                return new BackupResult { Success = false, Message = "备份文件不存在" };

            var result = await PostgresBackupUtility.RestoreBackupAsync(
                HardcodedConfig.PostgreSQLConnectionString,
                backupFilePath);

            if (result.Success)
            {
                Log.Information("数据库恢复成功: {File}", backupFilePath);
                return new BackupResult { Success = true, Message = result.Message };
            }
            else
            {
                Log.Error("数据库恢复失败: {Error}", result.Message);
                return new BackupResult { Success = false, Message = result.Message };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据库恢复异常");
            return new BackupResult { Success = false, Message = $"恢复异常: {ex.Message}" };
        }
    }

    /// <summary>
    /// 获取备份列表
    /// </summary>
    public async Task<List<BackupRecordDto>> GetBackupListAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

        var records = await dbContext.BackupRecords
            .AsNoTracking()
            .OrderByDescending(b => b.BackupTime)
            .ToListAsync();

        return records.Select(b => new BackupRecordDto
            {
                Id = b.Id,
                FileName = b.FileName,
                FilePath = ResolveBackupFilePath(b),
                BackupType = b.BackupType,
                FileSize = b.FileSize,
                BackupTime = b.BackupTime,
                ExpireTime = b.ExpireTime,
                Status = b.Status,
                Remark = b.Remark
            })
            .ToList();
    }

    /// <summary>
    /// 删除备份文件
    /// </summary>
    public async Task<BackupResult> DeleteBackupAsync(int backupId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

            var record = await dbContext.BackupRecords.FindAsync(backupId);
            if (record == null)
                return new BackupResult { Success = false, Message = "备份记录不存在" };

            var filePath = ResolveBackupFilePath(record);
            if (File.Exists(filePath))
                File.Delete(filePath);

            dbContext.BackupRecords.Remove(record);
            await dbContext.SaveChangesAsync();

            return new BackupResult { Success = true, Message = "删除成功" };
        }
        catch (Exception ex)
        {
            return new BackupResult { Success = false, Message = $"删除失败: {ex.Message}" };
        }
    }

    /// <summary>
    /// 执行 pg_dump 命令
    /// </summary>
    private async Task<bool> ExecutePgDumpAsync(string outputPath)
    {
        var connStr = HardcodedConfig.PostgreSQLConnectionString;
        if (string.IsNullOrEmpty(connStr))
        {
            Log.Error("PostgreSQL 连接字符串未配置");
            return false;
        }

        var connParams = ParseConnectionString(connStr);

        // 尝试多个可能的 pg_dump 路径
        var pgDumpPaths = new[]
        {
            "pg_dump",  // 系统 PATH
            @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\14\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\13\bin\pg_dump.exe",
            @"C:\Program Files (x86)\PostgreSQL\16\bin\pg_dump.exe",
            @"C:\Program Files (x86)\PostgreSQL\15\bin\pg_dump.exe",
        };

        foreach (var pgDumpPath in pgDumpPaths)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = $"--host={connParams.Host} --port={connParams.Port} --username={connParams.Username} --dbname={connParams.Database} --no-password --format=plain --encoding=UTF8",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrEmpty(connParams.Password))
                    psi.EnvironmentVariables["PGPASSWORD"] = connParams.Password;

                using var process = Process.Start(psi);
                if (process == null) continue;

                using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                await process.StandardOutput.BaseStream.CopyToAsync(outputStream);

                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Log.Information("pg_dump 执行成功: {Path}", pgDumpPath);
                    return true;
                }
                else
                {
                    Log.Warning("pg_dump 失败 (路径: {Path}): {Error}", pgDumpPath, error);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // pg_dump 不存在，尝试下一个路径
                continue;
            }
        }

        Log.Error("所有 pg_dump 路径均失败，请确保 PostgreSQL 客户端工具已安装");
        return false;
    }

    /// <summary>
    /// 清理过期备份
    /// </summary>
    private async Task CleanupExpiredBackupsAsync(ProDbContext dbContext)
    {
        var expiredRecords = await dbContext.BackupRecords
            .Where(r => r.ExpireTime <= DateTime.Now)
            .ToListAsync();

        foreach (var record in expiredRecords)
        {
            var filePath = ResolveBackupFilePath(record);
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "删除过期备份文件失败: {Path}", filePath);
            }
        }

        if (expiredRecords.Any())
        {
            dbContext.BackupRecords.RemoveRange(expiredRecords);
            await dbContext.SaveChangesAsync();
            Log.Information("清理了 {Count} 条过期备份记录", expiredRecords.Count);
        }
    }

    private static string ResolveBackupFilePath(BackupRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.FilePath))
            return record.FileName;

        return string.Equals(Path.GetFileName(record.FilePath), record.FileName, StringComparison.OrdinalIgnoreCase)
            ? record.FilePath
            : Path.Combine(record.FilePath, record.FileName);
    }

    /// <summary>
    /// 解析连接字符串
    /// </summary>
    private static PostgresConnectionParams ParseConnectionString(string connectionString)
    {
        var result = new PostgresConnectionParams();
        var pairs = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=', 2);
            if (keyValue.Length != 2) continue;

            var key = keyValue[0].Trim().ToLowerInvariant();
            var value = keyValue[1].Trim();

            switch (key)
            {
                case "host": result.Host = value; break;
                case "port": result.Port = value; break;
                case "database": result.Database = value; break;
                case "username": result.Username = value; break;
                case "password": result.Password = value; break;
            }
        }

        return result;
    }
}

public class PostgresConnectionParams
{
    public string Host { get; set; } = "localhost";
    public string Port { get; set; } = "5432";
    public string Database { get; set; } = "pro";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "";
}

public class BackupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? FileName { get; set; }
    public long FileSize { get; set; }
}

public class BackupRecordDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string BackupType { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime BackupTime { get; set; }
    public DateTime? ExpireTime { get; set; }
    public string Status { get; set; } = "";
    public string? Remark { get; set; }
}
