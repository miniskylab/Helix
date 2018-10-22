using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Helix
{
    public class Verifier
    {
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly HttpClient _httpClient;
        readonly string _reportFilePath = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Report.csv";
        readonly TextWriter _textWriter;

        public Verifier()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(Configurations.RequestTimeoutDuration) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Configurations.UserAgent);

            if (File.Exists(_reportFilePath)) File.Delete(_reportFilePath);
            _textWriter = new StreamWriter(_reportFilePath);

            var cancellationToken = _cancellationTokenSource.Token;
            Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    _textWriter.Flush();
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            }, cancellationToken);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _textWriter.Flush();
            _textWriter.Dispose();
        }

        public VerificationResult Verify(Resource resource)
        {
            var verificationResult = new VerificationResult
            {
                Uri = resource.Uri,
                IsInternalUrl = IsInternalUrl(resource)
            };

            try { verificationResult.StatusCode = (int) _httpClient.GetAsync(resource.Uri).Result.StatusCode; }
            catch (AggregateException aggregateException)
            {
                if (!(aggregateException.InnerException is TaskCanceledException)) throw;
                verificationResult.StatusCode = (int) HttpStatusCode.RequestTimeout;
            }

            Report(verificationResult);
            return verificationResult;
        }

        static bool IsInternalUrl(Resource resource)
        {
            return resource.Uri.Equals(Configurations.StartUri) ||
                   resource.Uri.Authority.ToLower().Equals(resource.ParentUri.Authority.ToLower()) ||
                   resource.Uri.Authority.ToLower().EndsWith(Configurations.TopLevelDomain.ToLower());
        }

        void Report(VerificationResult verificationResult)
        {
            if (Configurations.ReportBrokenLinksOnly && verificationResult.StatusCode < 400) return;
            _textWriter.WriteLine($"{verificationResult.StatusCode},{verificationResult.Uri}");
            Console.WriteLine($"{verificationResult.StatusCode} {verificationResult.Uri}");
        }
    }
}