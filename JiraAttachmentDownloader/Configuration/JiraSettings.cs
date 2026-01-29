using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraAttachmentDownloader.Configuration
{
    public class JiraSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string PersonalAccessToken { get; set; } = string.Empty;
        public string JqlQuery { get; set; } = string.Empty;
        public string DownloadPath { get; set; } = @"D:\downloads\attachments";
        public int MaxResultsPerPage { get; set; } = 50;
    }
}
