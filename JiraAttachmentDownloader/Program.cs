using JiraAttachmentDownloader.Configuration;
using JiraAttachmentDownloader.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JiraAttachmentDownloader
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                // Build configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build();

                // Setup dependency injection
                var services = new ServiceCollection();

                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConfiguration(configuration.GetSection("Logging"));
                    builder.AddConsole(options =>
                    {
                        options.FormatterName = "simple";
                    });
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                // Add configuration
                services.Configure<JiraSettings>(configuration.GetSection("JiraSettings"));

                // Validate settings
                var jiraSettings = configuration.GetSection("JiraSettings").Get<JiraSettings>();
                ValidateSettings(jiraSettings);

                // Add HttpClient
                services.AddHttpClient<IJiraService, JiraService>()
                    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        // Handle self-signed certificates if needed (remove in production if not required)
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    });

                // Add services
                services.AddTransient<AttachmentDownloader>();

                // Build service provider
                var serviceProvider = services.BuildServiceProvider();

                // Get the downloader and run
                var downloader = serviceProvider.GetRequiredService<AttachmentDownloader>();

                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    Console.WriteLine("\n⚠️ Cancellation requested. Please wait...");
                };

                await downloader.DownloadAllAttachmentsAsync(cts.Token);

                return 0;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("🛑 Operation was cancelled by user.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 Fatal Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1;
            }
        }

        private static void ValidateSettings(JiraSettings? settings)
        {
            var errors = new List<string>();

            if (settings == null)
            {
                throw new InvalidOperationException("JiraSettings section is missing from appsettings.json");
            }

            if (string.IsNullOrWhiteSpace(settings.BaseUrl))
                errors.Add("BaseUrl is required");

            if (string.IsNullOrWhiteSpace(settings.PersonalAccessToken))
                errors.Add("PersonalAccessToken is required");

            if (string.IsNullOrWhiteSpace(settings.JqlQuery))
                errors.Add("JqlQuery is required");

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Configuration validation failed:\n- {string.Join("\n- ", errors)}");
            }

            Console.WriteLine("✅ Configuration validated successfully");
        }
    }
}
