using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Helix.Bot.Abstractions;

namespace Helix.Bot
{
    public sealed class ResourceVerifier : IResourceVerifier
    {
        readonly HttpClient _httpClient;
        readonly IHttpContentTypeToResourceTypeDictionary _httpContentTypeToResourceTypeDictionary;
        readonly IResourceScope _resourceScope;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ResourceVerifier(IHttpContentTypeToResourceTypeDictionary httpContentTypeToResourceTypeDictionary, HttpClient httpClient,
            IResourceScope resourceScope)
        {
            _httpClient = httpClient;
            _resourceScope = resourceScope;
            _httpContentTypeToResourceTypeDictionary = httpContentTypeToResourceTypeDictionary;
        }

        public async Task<VerificationResult> Verify(Resource resource, CancellationToken cancellationToken)
        {
            var resourceIsAlreadyVerified = resource.StatusCode != default;
            if (resourceIsAlreadyVerified) return resource.ToVerificationResult();

            Task<HttpResponseMessage> sendingGETRequestTask = null;
            try
            {
                sendingGETRequestTask = _httpClient.GetAsync(
                    resource.Uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );

                var httpResponseMessage = await sendingGETRequestTask;
                var httpContentType = httpResponseMessage.Content.Headers.ContentType?.ToString();
                resource.Uri = httpResponseMessage.RequestMessage.RequestUri;
                resource.StatusCode = (StatusCode) httpResponseMessage.StatusCode;
                resource.Size = httpResponseMessage.Content.Headers.ContentLength;
                resource.ResourceType = _httpContentTypeToResourceTypeDictionary[httpContentType];

                if (!_resourceScope.IsStartUri(resource.OriginalUri))
                    resource.IsInternal = _resourceScope.IsInternalResource(resource);
            }
            catch (TaskCanceledException)
            {
                resource.StatusCode = cancellationToken.IsCancellationRequested
                    ? StatusCode.Processing
                    : StatusCode.RequestTimeout;
            }
            catch (HttpRequestException) { resource.StatusCode = StatusCode.Failed; }
            finally { sendingGETRequestTask?.Dispose(); }

            return resource.ToVerificationResult();
        }
    }
}