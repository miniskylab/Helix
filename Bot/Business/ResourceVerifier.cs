using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Helix.Bot.Abstractions;
using Helix.Core;
using log4net;

namespace Helix.Bot
{
    public sealed class ResourceVerifier : IResourceVerifier
    {
        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ResourceVerifier(IHttpContentTypeToResourceTypeDictionary httpContentTypeToResourceTypeDictionary, HttpClient httpClient,
            IResourceScope resourceScope, ILog log)
        {
            _log = log;
            _httpClient = httpClient;
            _resourceScope = resourceScope;
            _httpContentTypeToResourceTypeDictionary = httpContentTypeToResourceTypeDictionary;
        }

        public async Task<VerificationResult> Verify(Resource resource, CancellationToken cancellationToken)
        {
            var resourceIsAlreadyVerified = resource.StatusCode != default;
            if (resourceIsAlreadyVerified) return resource.ToVerificationResult();

            HttpResponseMessage httpResponseMessage = null;
            Task<HttpResponseMessage> sendingGETRequestTask = null;
            try
            {
                var uriBeingRendered = resource.Uri;
                while (sendingGETRequestTask == null || TryFollowRedirect())
                {
                    sendingGETRequestTask?.Dispose();
                    sendingGETRequestTask = _httpClient.GetAsync(
                        uriBeingRendered,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken
                    );
                    httpResponseMessage = await sendingGETRequestTask;
                }

                var httpContentType = httpResponseMessage.Content.Headers.ContentType?.ToString();
                resource.Uri = httpResponseMessage.RequestMessage.RequestUri.StripFragment();
                resource.StatusCode = (StatusCode) httpResponseMessage.StatusCode;
                resource.Size = httpResponseMessage.Content.Headers.ContentLength;
                resource.ResourceType = _httpContentTypeToResourceTypeDictionary[httpContentType];

                if (!_resourceScope.IsStartUri(resource.OriginalUri))
                    resource.IsInternal = _resourceScope.IsInternalResource(resource);

                #region Local Functions

                bool TryFollowRedirect()
                {
                    /* In dotnet Core, for security reason, HttpClient does not automatically follow HTTPS -> HTTP redirects.
                       https://github.com/dotnet/corefx/issues/24557 */

                    var responseStatusCode = (int) httpResponseMessage.StatusCode;
                    if (responseStatusCode < 300 || 400 <= responseStatusCode) return false;

                    if (httpResponseMessage.Headers.Location == null)
                        throw new HttpRedirectException(
                            $"Http redirect response without \"Location\" header detected while verifying: {resource.ToJson()}"
                        );

                    uriBeingRendered = httpResponseMessage.Headers.Location.IsAbsoluteUri
                        ? httpResponseMessage.Headers.Location
                        : new Uri(uriBeingRendered, httpResponseMessage.Headers.Location);

                    return true;
                }

                #endregion
            }
            catch (HttpRequestException) { resource.StatusCode = StatusCode.Failed; }
            catch (HttpRedirectException httpRedirectException)
            {
                resource.StatusCode = StatusCode.Failed;
                _log.Info(httpRedirectException.Message);
            }
            catch (Exception exception) when (exception is OperationCanceledException)
            {
                resource.StatusCode = cancellationToken.IsCancellationRequested
                    ? StatusCode.Processing
                    : StatusCode.RequestTimeout;
            }
            finally { sendingGETRequestTask?.Dispose(); }

            return resource.ToVerificationResult();
        }

        #region Injected Services

        readonly HttpClient _httpClient;
        readonly IHttpContentTypeToResourceTypeDictionary _httpContentTypeToResourceTypeDictionary;
        readonly IResourceScope _resourceScope;
        readonly ILog _log;

        #endregion
    }
}