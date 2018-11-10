using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CrawlerBackendBusiness
{
    sealed class ResourceVerifier : IDisposable
    {
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly Configurations _configurations;
        readonly HttpClient _httpClient;
        Task<HttpResponseMessage> _sendingGETRequestTask;

        public event IdleEvent OnIdle;

        public ResourceVerifier(Configurations configurations)
        {
            _configurations = configurations;
            _cancellationTokenSource = new CancellationTokenSource();

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(_configurations.RequestTimeoutDuration) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_configurations.UserAgent);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _sendingGETRequestTask?.Wait();
            _cancellationTokenSource?.Dispose();
            _httpClient?.Dispose();
        }

        public VerificationResult Verify(RawResource rawResource)
        {
            var verificationResult = new VerificationResult { RawResource = rawResource };
            if (!TryProcessRawResource(rawResource, out var resource))
            {
                verificationResult.Resource = null;
                verificationResult.StatusCode = (int) HttpStatusCode.ExpectationFailed;
                verificationResult.IsInternalResource = false;

                OnIdle?.Invoke();
                return verificationResult;
            }

            try
            {
                verificationResult.Resource = resource;
                verificationResult.IsInternalResource = IsInternalResource(resource);

                _sendingGETRequestTask = _httpClient.GetAsync(resource.Uri, _cancellationTokenSource.Token);
                verificationResult.StatusCode = (int) _sendingGETRequestTask.Result.StatusCode;
            }
            catch (AggregateException aggregateException)
            {
                switch (aggregateException.InnerException)
                {
                    case TaskCanceledException _:
                        verificationResult.StatusCode = _cancellationTokenSource.Token.IsCancellationRequested
                            ? (int) HttpStatusCode.Processing
                            : (int) HttpStatusCode.RequestTimeout;
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

        bool IsInternalResource(Resource resource)
        {
            return IsStartUrl(resource.Uri.AbsoluteUri) ||
                   resource.Uri.Authority.ToLower().Equals(resource.ParentUri.Authority.ToLower()) ||
                   resource.Uri.Authority.ToLower().EndsWith(_configurations.DomainName.ToLower());
        }

        bool IsStartUrl(string url) { return url.ToLower().EnsureEndsWith('/').Equals(_configurations.StartUrl.EnsureEndsWith('/')); }

        static void StripFragmentFrom(ref Uri uri)
        {
            if (string.IsNullOrWhiteSpace(uri.Fragment)) return;
            uri = new Uri(uri.AbsoluteUri.Replace(uri.Fragment, string.Empty));
        }

        bool TryProcessRawResource(RawResource rawResource, out Resource resource)
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

        public delegate void IdleEvent();
    }
}