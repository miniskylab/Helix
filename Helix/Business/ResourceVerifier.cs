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
    class ResourceVerifier : IDisposable
    {
        readonly HttpClient _httpClient;
        static TextWriter _textWriter;
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

        public event IdleEvent OnIdle;

        public ResourceVerifier()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(Configurations.RequestTimeoutDuration) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Configurations.UserAgent);

            FlushDataToDiskEvery(TimeSpan.FromSeconds(5));
        }

        static ResourceVerifier() { EnsureReportFileIsRecreated(); }

        public void Dispose()
        {
            CancellationTokenSource?.Cancel();
            _httpClient?.Dispose();

            _textWriter?.Flush();
            _textWriter?.Dispose();
            _textWriter = null;
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
                OnIdle?.Invoke();
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
                    default:
                        OnIdle?.Invoke();
                        throw;
                }
            }

            WriteReport(verificationResult);
            OnIdle?.Invoke();
            return verificationResult;
        }

        static string EnsureAbsolute(string possiblyRelativeUrl, Uri parentUri)
        {
            if (parentUri == null || !possiblyRelativeUrl.StartsWith("/")) return possiblyRelativeUrl;
            var baseString = possiblyRelativeUrl.StartsWith("//")
                ? $"{parentUri.Scheme}:"
                : $"{parentUri.Scheme}://{parentUri.Host}:{parentUri.Port}";
            return $"{baseString}/{possiblyRelativeUrl}";
        }

        static void EnsureReportFileIsRecreated()
        {
            var reportFilePath = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Report.csv";
            if (File.Exists(reportFilePath)) File.Delete(reportFilePath);
            _textWriter = TextWriter.Synchronized(new StreamWriter(reportFilePath));
        }

        static void FlushDataToDiskEvery(TimeSpan timeSpan)
        {
            var cancellationToken = CancellationTokenSource.Token;
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
            return IsStartUrl(resource.Uri.AbsoluteUri) ||
                   resource.Uri.Authority.ToLower().Equals(resource.ParentUri.Authority.ToLower()) ||
                   resource.Uri.Authority.ToLower().EndsWith(Configurations.TopLevelDomain.ToLower());
        }

        static bool IsStartUrl(string url) { return url.ToLower().EnsureEndsWith('/').Equals(Configurations.StartUrl.EnsureEndsWith('/')); }

        static void StripFragmentFrom(ref Uri uri)
        {
            if (string.IsNullOrWhiteSpace(uri.Fragment)) return;
            uri = new Uri(uri.AbsoluteUri.Replace(uri.Fragment, string.Empty));
        }

        static bool TryProcessRawResource(RawResource rawResource, out Resource resource)
        {
            resource = null;
            Uri parentUri = null;
            if (!IsStartUrl(rawResource.Url) && !Uri.TryCreate(rawResource.ParentUrl, UriKind.Absolute, out parentUri)) return false;

            var absoluteUrl = EnsureAbsolute(rawResource.Url, parentUri);
            if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri)) return false;
            StripFragmentFrom(ref uri);

            resource = new Resource { Uri = uri, ParentUri = parentUri };
            return true;
        }

        static void WriteReport(VerificationResult verificationResult)
        {
            if (Configurations.ReportBrokenLinksOnly && !verificationResult.IsBrokenResource) return;
            var verifiedUrl = verificationResult.Resource?.Uri.OriginalString ?? verificationResult.RawResource.Url;
            _textWriter.WriteLine($"{verificationResult.StatusCode},{verifiedUrl}");
            Console.WriteLine($"{verificationResult.StatusCode} {verifiedUrl}");
        }

        public delegate void IdleEvent();
    }
}