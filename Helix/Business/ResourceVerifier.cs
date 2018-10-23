using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Helix
{
    public class ResourceVerifier
    {
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly HttpClient _httpClient;
        readonly string _reportFilePath = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Report.csv";
        TextWriter _textWriter;

        public ResourceVerifier()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(Configurations.RequestTimeoutDuration) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Configurations.UserAgent);

            EnsureReportFileIsRecreated();
            FlushDataToDiskEvery(TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _textWriter.Flush();
            _textWriter.Dispose();
        }

        public VerificationResult Verify(RawResource rawResource)
        {
            var verificationResult = new VerificationResult { RawResource = rawResource };
            if (!TryProcessRawResource(rawResource, out var resource))
            {
                verificationResult.Resource = null;
                verificationResult.StatusCode = (int) HttpStatusCode.ExpectationFailed;
                verificationResult.IsInternalResource = false;

                WriteReport(verificationResult);
                return verificationResult;
            }

            try
            {
                verificationResult.Resource = resource;
                verificationResult.IsInternalResource = IsInternalResource(resource);
                verificationResult.StatusCode = (int) _httpClient.GetAsync(resource.Uri).Result.StatusCode;
            }
            catch (AggregateException aggregateException)
            {
                switch (aggregateException.InnerException)
                {
                    case TaskCanceledException _:
                        verificationResult.StatusCode = (int) HttpStatusCode.RequestTimeout;
                        break;
                    case HttpRequestException _:
                    case SocketException _:
                        verificationResult.StatusCode = (int) HttpStatusCode.BadRequest;
                        break;
                    default: throw;
                }
            }

            WriteReport(verificationResult);
            return verificationResult;
        }

        void EnsureReportFileIsRecreated()
        {
            if (File.Exists(_reportFilePath)) File.Delete(_reportFilePath);
            _textWriter = TextWriter.Synchronized(new StreamWriter(_reportFilePath));
        }

        void FlushDataToDiskEvery(TimeSpan timeSpan)
        {
            var cancellationToken = _cancellationTokenSource.Token;
            Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    _textWriter.Flush();
                    Thread.Sleep(timeSpan);
                }
            }, cancellationToken);
        }

        static bool IsInternalResource(Resource resource)
        {
            return resource.Uri.AbsoluteUri.ToLower().TrimEnd('/').Equals(Configurations.StartUrl.ToLower().TrimEnd('/')) ||
                   resource.Uri.Authority.ToLower().Equals(resource.ParentUri.Authority.ToLower()) ||
                   resource.Uri.Authority.ToLower().EndsWith(Configurations.TopLevelDomain.ToLower());
        }

        static bool TryProcessRawResource(RawResource rawResource, out Resource resource)
        {
            resource = null;
            Uri parentUri = null;
            var isStartUrl = string.Equals(rawResource.Url, Configurations.StartUrl, StringComparison.InvariantCultureIgnoreCase);
            if (!isStartUrl && !Uri.TryCreate(rawResource.ParentUrl, UriKind.Absolute, out parentUri)) return false;

            var absoluteUrl = rawResource.Url.ToLower();
            if (parentUri != null && absoluteUrl.StartsWith("/"))
            {
                var baseString = absoluteUrl.StartsWith("//")
                    ? $"{parentUri.Scheme}:"
                    : $"{parentUri.Scheme}://{parentUri.Host}:{parentUri.Port}";
                absoluteUrl = $"{baseString}/{absoluteUrl}";
            }
            if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri)) return false;
            if (!string.IsNullOrWhiteSpace(uri.Fragment)) uri = new Uri(absoluteUrl.Replace(uri.Fragment, string.Empty));

            resource = new Resource { Uri = uri, ParentUri = parentUri };
            return true;
        }

        void WriteReport(VerificationResult verificationResult)
        {
            if (Configurations.ReportBrokenLinksOnly && !verificationResult.IsBrokenResource) return;
            var verifiedUrl = verificationResult.Resource?.Uri.AbsoluteUri ?? verificationResult.RawResource.Url;
            _textWriter.WriteLine($"{verificationResult.StatusCode},{verifiedUrl}");
            Console.WriteLine($"{verificationResult.StatusCode} {verifiedUrl}");
        }
    }
}