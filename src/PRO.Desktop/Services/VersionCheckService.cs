using System.Reflection;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PRO.Desktop.Services;

public class VersionCheckService
{
    private readonly ProDbContext _dbContext;

    public static string LocalVersion =>
        Assembly.GetExecutingAssembly().GetName()?.Version?.ToString(3) ?? "1.0.0";

    public VersionCheckService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
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
