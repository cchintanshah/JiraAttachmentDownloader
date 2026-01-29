using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraAttachmentDownloader.Models;

namespace JiraAttachmentDownloader.Services
{
    public interface IJiraService
    {
        Task<List<JiraIssue>> SearchIssuesAsync(string jql, CancellationToken cancellationToken = default);
        Task<Stream> DownloadAttachmentAsync(string contentUrl, CancellationToken cancellationToken = default);
    }
}
