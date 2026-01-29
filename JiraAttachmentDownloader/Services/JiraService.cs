using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Json;
using System.Web;
using JiraAttachmentDownloader.Configuration;
using JiraAttachmentDownloader.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JiraAttachmentDownloader.Services
{
    public class JiraService : IJiraService
    {
        private readonly HttpClient _httpClient;
        private readonly JiraSettings _settings;
        private readonly ILogger<JiraService> _logger;

        public JiraService(
            HttpClient httpClient,
            IOptions<JiraSettings> settings,
            ILogger<JiraService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            // Configure HttpClient
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.PersonalAccessToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<JiraIssue>> SearchIssuesAsync(string jql, CancellationToken cancellationToken = default)
        {
            var allIssues = new List<JiraIssue>();
            int startAt = 0;
            int total;

            _logger.LogInformation("🔍 Starting JQL search: {Jql}", jql);

            do
            {
                var encodedJql = HttpUtility.UrlEncode(jql);
                var requestUrl = $"rest/api/2/search?jql={encodedJql}&startAt={startAt}&maxResults={_settings.MaxResultsPerPage}&fields=key,summary,attachment";

                _logger.LogDebug("📡 Requesting: {Url}", requestUrl);

                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("❌ API Error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"Jira API returned {response.StatusCode}: {errorContent}");
                }

                var searchResult = await response.Content.ReadFromJsonAsync<JiraSearchResult>(cancellationToken: cancellationToken);

                if (searchResult == null)
                {
                    _logger.LogWarning("⚠️ Received null response from Jira API");
                    break;
                }

                allIssues.AddRange(searchResult.Issues);
                total = searchResult.Total;
                startAt += searchResult.MaxResults;

                _logger.LogInformation("📊 Fetched {Count}/{Total} issues", allIssues.Count, total);

            } while (startAt < total);

            _logger.LogInformation("✅ Found {Count} total issues matching JQL", allIssues.Count);
            return allIssues;
        }

        public async Task<Stream> DownloadAttachmentAsync(string contentUrl, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync(contentUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
    }
}
