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
                _rawResourceProcessor.ProcessRawResource(rawResource, out var resource);
                verificationResult = new VerificationResult { RawResource = rawResource, Resource = resource };
                if (verificationResult.Resource.Uri == null) return true;

                verificationResult.IsInternalResource = _resourceScope.IsInternalResource(verificationResult.Resource);
                if (!verificationResult.IsInternalResource && !_configurations.VerifyExternalUrls)
                {
                    verificationResult = null;
                    return false;
                }

                if (verificationResult.Resource.HttpStatusCode != 0) return true;
                try
                {
                    _sendingGETRequestTask = _httpClient.GetAsync(verificationResult.Resource.Uri, _cancellationTokenSource.Token);
                    verificationResult.Resource.HttpStatusCode = (HttpStatusCode) _sendingGETRequestTask.Result.StatusCode;
                }
                catch (AggregateException aggregateException)
                {
                    switch (aggregateException.InnerException)
                    {
                        case TaskCanceledException _:
                            verificationResult.Resource.HttpStatusCode = _cancellationTokenSource.Token.IsCancellationRequested
                                ? HttpStatusCode.Processing
                                : HttpStatusCode.RequestTimeout;
                            break;
                        case HttpRequestException _:
                        case SocketException _:
                            verificationResult.Resource.HttpStatusCode = HttpStatusCode.BadRequest;
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