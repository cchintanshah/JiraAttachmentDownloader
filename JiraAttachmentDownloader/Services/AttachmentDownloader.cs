using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraAttachmentDownloader.Configuration;
using JiraAttachmentDownloader.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JiraAttachmentDownloader.Services
{
    public class AttachmentDownloader
    {
        private readonly IJiraService _jiraService;
        private readonly JiraSettings _settings;
        private readonly ILogger<AttachmentDownloader> _logger;

        public AttachmentDownloader(
            IJiraService jiraService,
            IOptions<JiraSettings> settings,
            ILogger<AttachmentDownloader> logger)
        {
            _jiraService = jiraService;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task DownloadAllAttachmentsAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("╔════════════════════════════════════════════════════════════╗");
            _logger.LogInformation("║        JIRA ATTACHMENT DOWNLOADER - Starting               ║");
            _logger.LogInformation("╚════════════════════════════════════════════════════════════╝");
            _logger.LogInformation("📁 Download Path: {Path}", _settings.DownloadPath);
            _logger.LogInformation("🔗 Jira URL: {Url}", _settings.BaseUrl);
            _logger.LogInformation("📝 JQL Query: {Jql}", _settings.JqlQuery);
            _logger.LogInformation("─────────────────────────────────────────────────────────────");

            // Ensure base download directory exists
            Directory.CreateDirectory(_settings.DownloadPath);

            // Search for issues
            var issues = await _jiraService.SearchIssuesAsync(_settings.JqlQuery, cancellationToken);

            var issuesWithAttachments = issues.Where(i => i.Fields?.Attachments?.Count > 0).ToList();
            var totalAttachments = issuesWithAttachments.Sum(i => i.Fields?.Attachments?.Count ?? 0);

            _logger.LogInformation("📊 Summary: {IssueCount} issues have {AttachmentCount} total attachments",
                issuesWithAttachments.Count, totalAttachments);
            _logger.LogInformation("─────────────────────────────────────────────────────────────");

            int downloadedCount = 0;
            int failedCount = 0;
            long totalBytes = 0;

            foreach (var issue in issuesWithAttachments)
            {
                var issueKey = issue.Key;
                var attachments = issue.Fields?.Attachments ?? new List<JiraAttachment>();

                _logger.LogInformation("");
                _logger.LogInformation("📌 Processing Issue: {Key} - {Summary}",
                    issueKey, TruncateString(issue.Fields?.Summary ?? "No Summary", 50));
                _logger.LogInformation("   📎 Attachments: {Count}", attachments.Count);

                // Create issue folder
                var issueFolderPath = Path.Combine(_settings.DownloadPath, issueKey);
                Directory.CreateDirectory(issueFolderPath);

                foreach (var attachment in attachments)
                {
                    var filePath = Path.Combine(issueFolderPath, SanitizeFileName(attachment.Filename));
                    var fileSize = FormatFileSize(attachment.Size);

                    try
                    {
                        _logger.LogInformation("   ⬇️  Downloading: {Filename} ({Size})", attachment.Filename, fileSize);

                        await using var stream = await _jiraService.DownloadAttachmentAsync(attachment.ContentUrl, cancellationToken);
                        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

                        var buffer = new byte[81920];
                        long totalRead = 0;
                        int bytesRead;
                        int lastPercentage = -1;

                        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                            totalRead += bytesRead;

                            if (attachment.Size > 0)
                            {
                                int percentage = (int)((totalRead * 100) / attachment.Size);
                                if (percentage != lastPercentage && percentage % 25 == 0)
                                {
                                    _logger.LogDebug("       Progress: {Percentage}%", percentage);
                                    lastPercentage = percentage;
                                }
                            }
                        }

                        downloadedCount++;
                        totalBytes += totalRead;
                        _logger.LogInformation("   ✅ Saved: {Path}", filePath);
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        _logger.LogError(ex, "   ❌ Failed to download {Filename}: {Message}", attachment.Filename, ex.Message);
                    }
                }
            }

            stopwatch.Stop();

            _logger.LogInformation("");
            _logger.LogInformation("═════════════════════════════════════════════════════════════");
            _logger.LogInformation("                    DOWNLOAD COMPLETE                        ");
            _logger.LogInformation("═════════════════════════════════════════════════════════════");
            _logger.LogInformation("✅ Downloaded: {Count} files", downloadedCount);
            _logger.LogInformation("❌ Failed: {Count} files", failedCount);
            _logger.LogInformation("💾 Total Size: {Size}", FormatFileSize(totalBytes));
            _logger.LogInformation("⏱️  Duration: {Duration}", stopwatch.Elapsed.ToString(@"hh\:mm\:ss"));
            _logger.LogInformation("📁 Location: {Path}", _settings.DownloadPath);
            _logger.LogInformation("═════════════════════════════════════════════════════════════");
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
        }
    }
}
