using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Helix.Crawler.Abstractions;
using Helix.Specifications.Core;
using Newtonsoft.Json;

namespace Helix.Crawler.Specifications
{
    internal class StartUrlDefinition : TheoryDescription<Configurations, string, bool, Type>
    {
        public StartUrlDefinition()
        {
            MatchConfiguredStartUrlUsingDomainName();
            MatchConfiguredStartUrlUsingIpAddress();
            ThrowExceptionIfArgumentNull();
            ThrowExceptionIfArgumentIsNotValid();
            IsNotStartUrlInAllOtherCases();
        }

        [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
        void IsNotStartUrlInAllOtherCases()
        {
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://192.168.1.2" },
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, "http://www.helix.com", false);
            AddTheoryDescription(p2: "http://www.helix.com/anything", p3: false);
        }

        void MatchConfiguredStartUrlUsingDomainName()
        {
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://www.helix.com" }
            }));
            AddTheoryDescription(configurations, "http://www.helix.com", true);
        }

        void MatchConfiguredStartUrlUsingIpAddress()
        {
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://192.168.1.2" },
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, "http://www.helix.com", true);
        }

        void ThrowExceptionIfArgumentIsNotValid() { AddTheoryDescription(p2: "ThisIsAnInvalidUrl", p4: typeof(UriFormatException)); }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p4: typeof(ArgumentNullException)); }
    }
}