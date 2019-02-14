using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class RawResourceVerifier : IRawResourceVerifier
    {
        CancellationTokenSource _cancellationTokenSource;
        readonly Configurations _configurations;
        readonly object _disposalSyncRoot;
        HttpClient _httpClient;
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
            _disposalSyncRoot = new object();

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(configurations.RequestTimeoutDuration) };
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("*");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("*");
            _httpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
            _httpClient.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.102 Safari/537.36"
            );
        }

        public void Dispose()
        {
            lock (_disposalSyncRoot)
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }
        }

        public bool TryVerify(RawResource rawResource, out VerificationResult verificationResult)
        {
            verificationResult = new VerificationResult { RawResource = rawResource };
            if (!_rawResourceProcessor.TryProcessRawResource(rawResource, out var resource))
            {
                verificationResult.Resource = null;
                verificationResult.HttpStatusCode = (int) HttpStatusCode.ExpectationFailed;
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
            verificationResult.HttpStatusCode = rawResource.HttpStatusCode;
            if (verificationResult.HttpStatusCode != 0) return true;

            try
            {
                _sendingGETRequestTask = _httpClient.GetAsync(resource.Uri, _cancellationTokenSource.Token);
                verificationResult.HttpStatusCode = (int) _sendingGETRequestTask.Result.StatusCode;
            }
            catch (AggregateException aggregateException)
            {
                switch (aggregateException.InnerException)
                {
                    case TaskCanceledException _:
                        verificationResult.HttpStatusCode = _cancellationTokenSource.Token.IsCancellationRequested
                            ? (int) HttpStatusCode.Processing
                            : (int) HttpStatusCode.RequestTimeout;
                        break;
                    case HttpRequestException _:
                    case SocketException _:
                        verificationResult.HttpStatusCode = (int) HttpStatusCode.BadRequest;
                        break;
                    default:
                        throw;
                }
            }
            return true;
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