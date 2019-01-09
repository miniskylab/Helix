using System;
using System.Net;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using HtmlAgilityPackDocument = HtmlAgilityPack.HtmlDocument;

namespace Helix.Crawler
{
    public class RawResourceExtractor : IRawResourceExtractor
    {
        readonly Configurations _configurations;
        static ProxyServer _httpProxyServer;
        readonly IResourceScope _resourceScope;
        readonly Func<string, bool> _urlSchemeIsSupported = url => url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                                                                   url.StartsWith("https", StringComparison.OrdinalIgnoreCase) ||
                                                                   url.StartsWith("/", StringComparison.OrdinalIgnoreCase);
        static readonly object StaticLock = new object();

        public event IdleEvent OnIdle;
        public event Action<RawResource> OnRawResourceExtracted;

        public RawResourceExtractor(Configurations configurations, IResourceScope resourceScope)
        {
            _configurations = configurations;
            _resourceScope = resourceScope;
            SetupHttpProxyServer();
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public void ExtractRawResourcesFrom(HtmlDocument htmlDocument)
        {
            if (htmlDocument == null) throw new ArgumentNullException();
            var htmlAgilityPackDocument = new HtmlAgilityPackDocument();
            htmlAgilityPackDocument.LoadHtml(htmlDocument.Text);

            var anchorTags = htmlAgilityPackDocument.DocumentNode.SelectNodes("//a[@href]");
            if (anchorTags == null) return;
            Parallel.ForEach(anchorTags, anchorTag =>
            {
                var url = anchorTag.Attributes["href"].Value;
                if (_urlSchemeIsSupported(url))
                    OnRawResourceExtracted?.Invoke(new RawResource
                    {
                        ParentUri = htmlDocument.Uri,
                        Url = url
                    });
            });
            OnIdle?.Invoke();
        }

        static void ReleaseUnmanagedResources()
        {
            lock (StaticLock)
            {
                _httpProxyServer?.Stop();
                _httpProxyServer?.Dispose();
                _httpProxyServer = null;
            }
        }

        void SetupHttpProxyServer()
        {
            if (_configurations.HttpProxyPort <= 0) return;
            lock (StaticLock)
            {
                if (_httpProxyServer != null) return;
                _httpProxyServer = new ProxyServer();
                _httpProxyServer.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Loopback, _configurations.HttpProxyPort));
                _httpProxyServer.Start();
                _httpProxyServer.BeforeRequest += EnsureInternal;
                _httpProxyServer.BeforeResponse += CaptureNetworkTraffic;
            }

            async Task CaptureNetworkTraffic(object _, SessionEventArgs networkTraffic)
            {
                await Task.Run(() =>
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
                    var newRawResource = new RawResource
                    {
                        ParentUri = request.RequestUri,
                        Url = request.Url,
                        HttpStatusCode = response.StatusCode
                    };
                    OnRawResourceExtracted?.Invoke(newRawResource);
                });
            }
            async Task EnsureInternal(object _, SessionEventArgs networkTraffic)
            {
                await Task.Run(() =>
                {
                    networkTraffic.WebSession.Request.RequestUri = _resourceScope.Localize(networkTraffic.WebSession.Request.RequestUri);
                    networkTraffic.WebSession.Request.Host = networkTraffic.WebSession.Request.RequestUri.Host;
                });
            }
        }

        ~RawResourceExtractor() { ReleaseUnmanagedResources(); }
    }
}