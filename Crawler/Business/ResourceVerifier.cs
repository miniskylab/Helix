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
        readonly Configurations _configurations;
        readonly HttpClient _httpClient;
        readonly IHttpContentTypeToResourceTypeDictionary _httpContentTypeToResourceTypeDictionary;
        bool _objectDisposed;
        Task<HttpResponseMessage> _sendingGETRequestTask;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ResourceVerifier(IHttpContentTypeToResourceTypeDictionary httpContentTypeToResourceTypeDictionary,
            Configurations configurations, HttpClient httpClient)
        {
            _configurations = configurations;
            _httpContentTypeToResourceTypeDictionary = httpContentTypeToResourceTypeDictionary;
            _objectDisposed = false;
            _httpClient = httpClient;
        }

        public void Dispose()
        {
            if (_objectDisposed) return;

            try { _sendingGETRequestTask?.Wait(); }
            catch
            {
                /* At this point, all exceptions should be fully handled.
                 * I just want to wait for the task to complete.
                 * I don't care about the result of the task. */
            }

            _sendingGETRequestTask?.Dispose();
            _objectDisposed = true;
        }

        // TODO: Refactor this method so that, it returns Task<bool>. We can then have a singleton ResourceVerifier
        public bool TryVerify(Resource resource, CancellationToken cancellationToken, out VerificationResult verificationResult)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ResourceVerifier));

            verificationResult = null;
            if (!resource.IsInternal && !_configurations.VerifyExternalUrls) return false;

            var resourceIsAlreadyVerified = resource.StatusCode != default;
            if (resourceIsAlreadyVerified)
            {
                verificationResult = resource.ToVerificationResult();
                return true;
            }

            try
            {
                _sendingGETRequestTask = _httpClient.GetAsync(
                    resource.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );

                var httpResponseMessage = _sendingGETRequestTask.Result;
                var httpContentType = httpResponseMessage.Content.Headers.ContentType?.ToString();
                resource.StatusCode = (StatusCode) httpResponseMessage.StatusCode;
                resource.Size = httpResponseMessage.Content.Headers.ContentLength;
                resource.ResourceType = _httpContentTypeToResourceTypeDictionary[httpContentType];
            }
            catch (AggregateException aggregateException)
            {
                switch (aggregateException.InnerException)
                {
                    case TaskCanceledException _:
                        resource.StatusCode = cancellationToken.IsCancellationRequested
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
            finally
            {
                verificationResult = resource.ToVerificationResult();
            }

            return true;
        }
    }
}