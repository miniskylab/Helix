using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Helix.Abstractions;

namespace Helix.Crawler
{
    sealed class ResourceVerifier : IResourceVerifier
    {
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly HttpClient _httpClient;
        readonly IRawResourceProcessor _rawResourceProcessor;
        readonly IResourceScopeIdentifier _resourceScopeIdentifier;
        Task<HttpResponseMessage> _sendingGETRequestTask;

        public event IdleEvent OnIdle;

        public ResourceVerifier(IConfigurations configurations, IRawResourceProcessor rawResourceProcessor,
            IResourceScopeIdentifier resourceScopeIdentifier)
        {
            _rawResourceProcessor = rawResourceProcessor;
            _resourceScopeIdentifier = resourceScopeIdentifier;
            _cancellationTokenSource = new CancellationTokenSource();

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(configurations.RequestTimeoutDuration) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(configurations.UserAgent);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _sendingGETRequestTask?.Wait();
            _cancellationTokenSource?.Dispose();
            _httpClient?.Dispose();
        }

        public IVerificationResult Verify(IRawResource rawResource)
        {
            var verificationResult = new VerificationResult { RawResource = rawResource };
            if (!_rawResourceProcessor.TryProcessRawResource(rawResource, out var resource))
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
                verificationResult.IsInternalResource = _resourceScopeIdentifier.IsInternalResource(resource);

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
    }
}