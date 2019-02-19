using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class RawResourceVerifier : IRawResourceVerifier
    {
        CancellationTokenSource _cancellationTokenSource;
        readonly Configurations _configurations;
        HttpClient _httpClient;
        bool _objectDisposed;
        readonly Dictionary<string, object> _publicApiLockMap;
        readonly IRawResourceProcessor _rawResourceProcessor;
        readonly IResourceScope _resourceScope;
        Task<HttpResponseMessage> _sendingGETRequestTask;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public RawResourceVerifier(Configurations configurations, IRawResourceProcessor rawResourceProcessor, IResourceScope resourceScope)
        {
            _configurations = configurations;
            _rawResourceProcessor = rawResourceProcessor;
            _resourceScope = resourceScope;
            _cancellationTokenSource = new CancellationTokenSource();
            _objectDisposed = false;
            _publicApiLockMap = new Dictionary<string, object> { { $"{nameof(TryVerify)}", new object() } };

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(configurations.RequestTimeoutDuration) };
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("*");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("*");
            _httpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
            _httpClient.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/72.0.3626.109 Safari/537.36"
            );
        }

        public void Dispose()
        {
            try
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Enter(lockObject);
                if (_objectDisposed) return;
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
                _objectDisposed = true;
            }
            finally
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Exit(lockObject);
            }
        }

        public bool TryVerify(RawResource rawResource, out VerificationResult verificationResult)
        {
            lock (_publicApiLockMap[nameof(TryVerify)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(RawResourceVerifier));
                verificationResult = new VerificationResult { RawResource = rawResource };
                var rawResourceProcessingResultCode = _rawResourceProcessor.TryProcessRawResource(rawResource, out var resource);
                if (rawResourceProcessingResultCode != HttpStatusCode.OK)
                {
                    verificationResult.Resource = null;
                    verificationResult.StatusCode = rawResourceProcessingResultCode;
                    verificationResult.IsInternalResource = false;
                    return true;
                }

                verificationResult.IsInternalResource = _resourceScope.IsInternalResource(resource);
                if (!verificationResult.IsInternalResource && !_configurations.VerifyExternalUrls)
                {
                    verificationResult = null;
                    return false;
                }

                verificationResult.Resource = resource;
                verificationResult.StatusCode = rawResource.HttpStatusCode;
                if (verificationResult.StatusCode != 0) return true;

                try
                {
                    _sendingGETRequestTask = _httpClient.GetAsync(resource.Uri, _cancellationTokenSource.Token);
                    verificationResult.StatusCode = (HttpStatusCode) _sendingGETRequestTask.Result.StatusCode;
                }
                catch (AggregateException aggregateException)
                {
                    switch (aggregateException.InnerException)
                    {
                        case TaskCanceledException _:
                            verificationResult.StatusCode = _cancellationTokenSource.Token.IsCancellationRequested
                                ? HttpStatusCode.Processing
                                : HttpStatusCode.RequestTimeout;
                            break;
                        case HttpRequestException _:
                        case SocketException _:
                            verificationResult.StatusCode = HttpStatusCode.BadRequest;
                            break;
                        default:
                            throw;
                    }
                }
                return true;
            }
        }

        void ReleaseUnmanagedResources()
        {
            _cancellationTokenSource?.Cancel();
            try { _sendingGETRequestTask?.Wait(); }
            catch
            {
                /* At this point, all exceptions should be fully handled.
                 * I just want to wait for the task to complete.
                 * I don't care about the result of the task. */
            }

            _sendingGETRequestTask?.Dispose();
            _cancellationTokenSource?.Dispose();
            _httpClient?.Dispose();

            _cancellationTokenSource = null;
            _sendingGETRequestTask = null;
            _httpClient = null;
        }

        ~RawResourceVerifier() { Dispose(); }
    }
}