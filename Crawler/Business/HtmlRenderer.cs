﻿using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using Helix.WebBrowser.Abstractions;
using Titanium.Web.Proxy.EventArguments;

namespace Helix.Crawler
{
    public class HtmlRenderer : IHtmlRenderer
    {
        readonly object _disposalSyncRoot;
        IWebBrowser _webBrowser;

        public event Action<RawResource> OnRawResourceCaptured;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HtmlRenderer(Configurations configurations, IWebBrowserProvider webBrowserProvider, IResourceScope resourceScope)
        {
            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var pathToChromiumExecutable = Path.Combine(workingDirectory, "chromium/chrome.exe");
            _webBrowser = webBrowserProvider.GetWebBrowser(
                pathToChromiumExecutable,
                workingDirectory,
                configurations.UseIncognitoWebBrowser,
                configurations.UseHeadlessWebBrowsers,
                (1920, 1080)
            );
            _webBrowser.BeforeRequest += EnsureInternal;
            _webBrowser.BeforeResponse += CaptureNetworkTraffic;
            _disposalSyncRoot = new object();

            Task EnsureInternal(object _, SessionEventArgs networkTraffic)
            {
                return Task.Run(() =>
                {
                    networkTraffic.WebSession.Request.RequestUri = resourceScope.Localize(networkTraffic.WebSession.Request.RequestUri);
                    networkTraffic.WebSession.Request.Host = networkTraffic.WebSession.Request.RequestUri.Host;
                });
            }
            Task CaptureNetworkTraffic(object _, SessionEventArgs networkTraffic)
            {
                var parentUri = _webBrowser.CurrentUri;
                return Task.Run(() =>
                {
                    var response = networkTraffic.WebSession.Response;
                    if (response.ContentType == null) return;

                    var request = networkTraffic.WebSession.Request;
                    var isNotGETRequest = request.Method.ToUpperInvariant() != "GET";
                    var isNotCss = !response.ContentType.StartsWith("text/css", StringComparison.OrdinalIgnoreCase);
                    var isNotImage = !response.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
                    var isNotAudio = !response.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
                    var isNotVideo = !response.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
                    var isNotFont = !response.ContentType.StartsWith("font/", StringComparison.OrdinalIgnoreCase);
                    var isNotJavaScript = !response.ContentType.StartsWith("application/javascript", StringComparison.OrdinalIgnoreCase) &&
                                          !response.ContentType.StartsWith("application/ecmascript", StringComparison.OrdinalIgnoreCase);
                    if (isNotGETRequest || isNotCss && isNotFont && isNotJavaScript && isNotImage && isNotAudio && isNotVideo) return;
                    OnRawResourceCaptured?.Invoke(new RawResource
                    {
                        ParentUri = parentUri,
                        Url = request.Url,
                        HttpStatusCode = (HttpStatusCode) response.StatusCode
                    });
                });
            }
        }

        public void Dispose()
        {
            lock (_disposalSyncRoot)
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }
        }

        public bool TryRender(Uri uri, Action<Exception> onFailed, CancellationToken cancellationToken, out string html,
            out long? pageLoadTime, int attemptCount = 3)
        {
            return _webBrowser.TryRender(uri, onFailed, cancellationToken, out html, out pageLoadTime, attemptCount);
        }

        void ReleaseUnmanagedResources()
        {
            _webBrowser?.Dispose();
            _webBrowser = null;
        }

        ~HtmlRenderer() { ReleaseUnmanagedResources(); }
    }
}