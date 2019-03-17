using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class ResourceVerifier : IResourceVerifier
    {
        CancellationTokenSource _cancellationTokenSource;
        readonly Configurations _configurations;
        HttpClient _httpClient;
        bool _objectDisposed;
        readonly IResourceProcessor _resourceProcessor;
        Task<HttpResponseMessage> _sendingGETRequestTask;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ResourceVerifier(Configurations configurations, IResourceProcessor resourceProcessor, HttpClient httpClient)
        {
            _configurations = configurations;
            _resourceProcessor = resourceProcessor;
            _cancellationTokenSource = new CancellationTokenSource();
            _objectDisposed = false;
            _httpClient = httpClient;
        }

        public void Dispose()
        {
            if (_objectDisposed) return;
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
            _objectDisposed = true;
        }

        public bool TryVerify(Resource resource, out VerificationResult verificationResult)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ResourceVerifier));
            verificationResult = null;

            _resourceProcessor.Enrich(resource);
            if (!resource.IsInternal && !_configurations.VerifyExternalUrls) return false;

            verificationResult = new VerificationResult
            {
                Id = resource.Id,
                IsInternalResource = resource.IsInternal,
                ParentUrl = resource.ParentUri?.AbsoluteUri,
                StatusCode = resource.StatusCode,
                VerifiedUrl = resource.AbsoluteUrl
            };
            if (resource.StatusCode != default) return true;

            try
            {
                _sendingGETRequestTask = _httpClient.GetAsync(
                    resource.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    _cancellationTokenSource.Token
                );
                var httpResponseMessage = _sendingGETRequestTask.Result;
                resource.StatusCode = (StatusCode) httpResponseMessage.StatusCode;
                resource.Size = httpResponseMessage.Content.Headers.ContentLength;
                _resourceProcessor.Categorize(resource, httpResponseMessage.Content.Headers.ContentType?.ToString());
            }
            catch (AggregateException aggregateException)
            {
                switch (aggregateException.InnerException)
                {
                    case TaskCanceledException _:
                        resource.StatusCode = _cancellationTokenSource.Token.IsCancellationRequested
                            ? StatusCode.Processing
                            : StatusCode.RequestTimeout;
                        break;
                    case HttpRequestException _:
                    case SocketException _:
                        resource.StatusCode = StatusCode.BadRequest;
                        break;
                    default:
                        throw;
                }
            }
            finally { verificationResult.StatusCode = resource.StatusCode; }
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

        ~ResourceVerifier() { ReleaseUnmanagedResources(); }
    }
}