using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PRO.Desktop.Services;

public class VersionCheckService
{
    private readonly ProDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public static string LocalVersion =>
        Assembly.GetExecutingAssembly().GetName()?.Version?.ToString(3) ?? "1.0.0";

    /// <summary>获取更新下载目录</summary>
    public static string UpdateDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PRO", "Updates");

    public VersionCheckService(ProDbContext dbContext, IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AppVersion?> CheckLatestVersionAsync()
    {
        try
        {
            var record = await _dbContext.AppVersions
                .OrderByDescending(v => v.Id)
                .FirstOrDefaultAsync();

            if (record == null) return null;

            var version = new AppVersion
            {
                Version = record.Version,
                ReleaseDate = record.ReleaseDate ?? "",
                ReleaseNotes = record.ReleaseNotes ?? "",
                DownloadUrl = record.DownloadUrl ?? "",
                IsRequired = record.IsRequired
            };

            if (string.IsNullOrEmpty(version.Version))
                return null;

            var localVer = ParseVersion(LocalVersion);
            var remoteVer = ParseVersion(version.Version);

            if (remoteVer == null || localVer == null)
                return null;

            return remoteVer > localVer ? version : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsVersionSuppressedAsync(string remoteVersion)
    {
        var setting = await _dbContext.LocalSettings
            .FirstOrDefaultAsync(s => s.SettingKey == "SuppressedVersion");
        return setting?.SettingValue == remoteVersion;
    }

    public async Task SuppressVersionAsync(string remoteVersion)
    {
        var setting = await _dbContext.LocalSettings
            .FirstOrDefaultAsync(s => s.SettingKey == "SuppressedVersion");
        if (setting == null)
        {
            _dbContext.LocalSettings.Add(new Domain.Entities.LocalSetting
            {
                SettingKey = "SuppressedVersion",
                SettingValue = remoteVersion,
                SettingType = "String",
                UpdatedAt = DateTime.Now
            });
        }
        else
        {
            setting.SettingValue = remoteVersion;
            setting.UpdatedAt = DateTime.Now;
        }
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// 后台下载新版本到本地缓存目录
    /// </summary>
    public async Task<(bool Success, string? FilePath)> DownloadUpdateAsync(AppVersion version, IProgress<int>? progress = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(version.DownloadUrl))
                return (false, null);

            Directory.CreateDirectory(UpdateDir);

            var fileName = $"PRO_v{version.Version}.zip";
            var filePath = Path.Combine(UpdateDir, fileName);

            // 如果已下载过，直接返回
            if (File.Exists(filePath))
            {
                progress?.Report(100);
                return (true, filePath);
            }

            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(version.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            using var stream = await response.Content.ReadAsStreamAsync();

            var tempPath = filePath + ".tmp";
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    var pct = (int)(totalRead * 100 / totalBytes);
                    progress.Report(pct);
                }
            }

            File.Move(tempPath, filePath, overwrite: true);

            // 清理旧版本文件
            foreach (var oldFile in Directory.GetFiles(UpdateDir, "PRO_v*.zip"))
            {
                if (oldFile != filePath)
                {
                    try { File.Delete(oldFile); } catch { }
                }
            }

            progress?.Report(100);
            return (true, filePath);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "下载更新失败: {Version}", version.Version);
            return (false, null);
        }
    }

    /// <summary>
    /// 检查是否有已下载的更新包
    /// </summary>
    public static string? GetDownloadedUpdatePath(string targetVersion)
    {
        var filePath = Path.Combine(UpdateDir, $"PRO_v{targetVersion}.zip");
        return File.Exists(filePath) ? filePath : null;
    }

    /// <summary>
    /// 应用更新：解压并启动更新脚本
    /// </summary>
    public static void ApplyUpdate(string zipPath)
    {
        var extractDir = Path.Combine(UpdateDir, "Staging");
        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, true);
        Directory.CreateDirectory(extractDir);

        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

        // 创建更新批处理脚本
        var appDir = AppContext.BaseDirectory;
        var updateScript = Path.Combine(extractDir, "update.bat");
        var scriptContent = $"""
            @echo off
            chcp 65001 >nul
            echo 正在更新 PRO 企业管理系统...
            timeout /t 2 /nobreak >nul
            taskkill /f /im PRO.exe >nul 2>&1
            timeout /t 1 /nobreak >nul
            xcopy /Y /E "{extractDir}\\*" "{appDir}" >nul
            echo 更新完成，正在启动...
            start "" "{appDir}\\PRO.exe"
            del "%~f0"
            """;

        File.WriteAllText(updateScript, scriptContent, System.Text.Encoding.UTF8);

        Process.Start(new ProcessStartInfo
        {
            FileName = updateScript,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static Version? ParseVersion(string v)
    {
        if (Version.TryParse(v, out var result))
            return result;
        return null;
    }
}

public class AppVersion
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}
