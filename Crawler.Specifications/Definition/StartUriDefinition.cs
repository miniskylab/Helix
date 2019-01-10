using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Newtonsoft.Json;

namespace Helix.Crawler.Specifications
{
    class StartUriDefinition : TheoryDescription<Configurations, string, bool, Type>
    {
        public StartUriDefinition()
        {
            MatchConfiguredStartUri();
            ThrowExceptionIfArgumentNull();
            ThrowExceptionIfArgumentIsNotValid();
            IsNotStartUriInAllOtherCases();
        }

        [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
        void IsNotStartUriInAllOtherCases()
        {
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://192.168.1.2" },
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, "http://www.helix.com", false);
            AddTheoryDescription(p2: "http://www.helix.com/anything", p3: false);
        }

        void MatchConfiguredStartUri()
        {
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://192.168.1.2" }
            }));
            AddTheoryDescription(configurations, "http://192.168.1.2", true);
            AddTheoryDescription(configurations, "http://192.168.1.2:80", true);

            configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://www.helix.com/anything" }
            }));
            AddTheoryDescription(configurations, "http://www.helix.com/anything", true);
        }

        void ThrowExceptionIfArgumentIsNotValid() { AddTheoryDescription(p2: "ThisIsAnInvalidUri", p4: typeof(UriFormatException)); }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p4: typeof(ArgumentNullException)); }
    }
}