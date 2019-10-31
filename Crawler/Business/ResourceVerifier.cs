using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class ResourceVerifier : IResourceVerifier
    {
        readonly HttpClient _httpClient;
        readonly IHttpContentTypeToResourceTypeDictionary _httpContentTypeToResourceTypeDictionary;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ResourceVerifier(IHttpContentTypeToResourceTypeDictionary httpContentTypeToResourceTypeDictionary, HttpClient httpClient)
        {
            _httpContentTypeToResourceTypeDictionary = httpContentTypeToResourceTypeDictionary;
            _httpClient = httpClient;
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
                resource.StatusCode = (StatusCode) httpResponseMessage.StatusCode;
                resource.Size = httpResponseMessage.Content.Headers.ContentLength;
                resource.ResourceType = _httpContentTypeToResourceTypeDictionary[httpContentType];
            }
            catch (TaskCanceledException)
            {
                resource.StatusCode = cancellationToken.IsCancellationRequested
                    ? StatusCode.Processing
                    : StatusCode.RequestTimeout;
            }
            finally
            {
                sendingGETRequestTask?.Dispose();
            }

            return resource.ToVerificationResult();
        }
    }
}