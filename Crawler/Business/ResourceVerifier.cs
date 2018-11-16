using System;
using System.Net;
using System.Net.Http;
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
        Task<HttpResponseMessage> _sendingHEADRequestTask;

        public event IdleEvent OnIdle;

        public ResourceVerifier(Configurations configurations, IResourceProcessor resourceProcessor, IResourceScope resourceScope)
        {
            _resourceProcessor = resourceProcessor;
            _resourceScope = resourceScope;
            _cancellationTokenSource = new CancellationTokenSource();

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(configurations.RequestTimeoutDuration) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(configurations.UserAgent);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cancellationTokenSource?.Cancel();
            _sendingHEADRequestTask?.Wait();
            _cancellationTokenSource?.Dispose();
            _httpClient?.Dispose();
        }

        public IVerificationResult Verify(IRawResource rawResource)
        {
            var verificationResult = new VerificationResult { RawResource = rawResource };
            if (!_resourceProcessor.TryProcessRawResource(rawResource, out var resource))
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
                verificationResult.IsInternalResource = _resourceScope.IsInternalResource(resource);

                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Head, resource.Uri);
                _sendingHEADRequestTask = _httpClient.SendAsync(httpRequestMessage, _cancellationTokenSource.Token);
                verificationResult.StatusCode = (int) _sendingHEADRequestTask.Result.StatusCode;
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
    }
}