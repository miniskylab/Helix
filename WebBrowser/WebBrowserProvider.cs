using System;
using Helix.Core;
using Helix.WebBrowser.Abstractions;

namespace Helix.WebBrowser
{
    public class WebBrowserProvider : IWebBrowserProvider
    {
        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public WebBrowserProvider() { }

        public IWebBrowser GetWebBrowser(bool useIncognitoWebBrowser, bool useHeadlessWebBrowser)
        {
            return new ChromiumWebBrowser(useIncognitoWebBrowser, useHeadlessWebBrowser);
        }
    }
}