using System;
using System.Collections.Generic;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Newtonsoft.Json;

namespace Helix.Crawler.Specifications
{
    internal class StartUriDescription : TheoryDescription<Configurations, Uri, bool, Type>
    {
        public StartUriDescription()
        {
            MatchConfiguredStartUri();

            IsNotStartUriInAllOtherCases();

            ThrowExceptionIfArgumentNull();
        }

        void IsNotStartUriInAllOtherCases()
        {
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://192.168.1.2" },
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, new Uri("http://www.helix.com"));
            AddTheoryDescription(p2: new Uri("http://www.helix.com/anything"));
        }

        void MatchConfiguredStartUri()
        {
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://192.168.1.2" }
            }));
            AddTheoryDescription(configurations, new Uri("http://192.168.1.2"), true);
            AddTheoryDescription(configurations, new Uri("http://192.168.1.2:80"), true);

            configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://www.helix.com/anything" }
            }));
            AddTheoryDescription(configurations, new Uri("http://www.helix.com/anything"), true);
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p4: typeof(ArgumentNullException)); }
    }
}