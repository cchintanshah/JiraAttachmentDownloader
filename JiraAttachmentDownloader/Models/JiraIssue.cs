using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace JiraAttachmentDownloader.Models
{
    public class JiraIssue
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("self")]
        public string Self { get; set; } = string.Empty;

        [JsonPropertyName("fields")]
        public JiraIssueFields? Fields { get; set; }
    }

    public class JiraIssueFields
    {
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("attachment")]
        public List<JiraAttachment> Attachments { get; set; } = new();
    }
}
