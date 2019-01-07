using System;
using System.Collections.Generic;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Newtonsoft.Json;

namespace Helix.Crawler.Specifications
{
    class UriLocalizationDefinition : TheoryDescription<Configurations, Uri, Uri, Type>
    {
        public UriLocalizationDefinition()
        {
            ReplaceDomainNameMatchingConfiguredWithStartUrlAuthority();
            DoesNothingToUriWhoseAuthorityIsDifferentFromTheConfiguredDomainName();
            ThrowExceptionIfArgumentNull();
        }

        void DoesNothingToUriWhoseAuthorityIsDifferentFromTheConfiguredDomainName()
        {
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, new Uri("http://www.sanity.com/anything"), new Uri("http://www.sanity.com/anything"));
        }

        void ReplaceDomainNameMatchingConfiguredWithStartUrlAuthority()
        {
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://192.168.1.2" },
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, new Uri("http://www.helix.com/anything"), new Uri("http://192.168.1.2/anything"));
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(p2: null, p4: typeof(ArgumentNullException)); }
    }
}