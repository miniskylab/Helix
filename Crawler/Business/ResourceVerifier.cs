using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Helix.Abstractions;
using JetBrains.Annotations;

namespace Helix.Implementations
{
    [UsedImplicitly]
    sealed class ResourceVerifier : IResourceVerifier
    {
        readonly CancellationTokenSource _cancellationTokenSource;
        bool _disposed;
        readonly HttpClient _httpClient;
        readonly IResourceProcessor _resourceProcessor;
        readonly IResourceScope _resourceScope;
        Task<HttpResponseMessage> _sendingGETRequestTask;

        public event IdleEvent OnIdle;

        public ResourceVerifier(Configurations configurations, IResourceProcessor resourceProcessor, IResourceScope resourceScope)
        {
            _resourceProcessor = resourceProcessor;
            _resourceScope = resourceScope;
            _cancellationTokenSource = new CancellationTokenSource();

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
            if (_disposed) return;
            _disposed = true;

            _cancellationTokenSource?.Cancel();
            _sendingGETRequestTask?.Wait();
            _cancellationTokenSource?.Dispose();
            _httpClient?.Dispose();
        }

        public IVerificationResult Verify(IRawResource rawResource)
        {
            var verificationResult = new VerificationResult { RawResource = rawResource };
            if (!_resourceProcessor.TryProcessRawResource(rawResource, out var resource))
            {
                verificationResult.Resource = null;
                verificationResult.HttpStatusCode = (int) HttpStatusCode.ExpectationFailed;
                verificationResult.IsInternalResource = false;

                OnIdle?.Invoke();
                return verificationResult;
            }

            try
            {
                verificationResult.Resource = resource;
                verificationResult.IsInternalResource = _resourceScope.IsInternalResource(resource);
                verificationResult.HttpStatusCode = rawResource.HttpStatusCode;
                if (verificationResult.HttpStatusCode == 0)
                {
                    _sendingGETRequestTask = _httpClient.GetAsync(resource.Uri, _cancellationTokenSource.Token);
                    verificationResult.HttpStatusCode = (int) _sendingGETRequestTask.Result.StatusCode;
                }
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
                        OnIdle?.Invoke();
                        throw;
                }
            }

            OnIdle?.Invoke();
            return verificationResult;
        }
    }
}