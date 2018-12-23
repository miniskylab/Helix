using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Helix.Crawler.Abstractions;
using Helix.Specifications.Core;
using Newtonsoft.Json;

namespace Helix.Crawler.Specifications
{
    internal class StartUrlDefinition : TheoryData<string, Configurations, bool, Type>
    {
        public StartUrlDefinition()
        {
            MatchConfiguredStartUrl();
            ThrowExceptionIfArgumentNull();
            IsNotStartUrlInAllOtherCases();
        }

        [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
        void IsNotStartUrlInAllOtherCases()
        {
            Add("http://192.168.1.2:8080", p3: false);
            Add("http://www.helix.com/anything", p3: false);
            Add(string.Empty, p3: false);

            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://192.168.1.2:8080" },
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            Add("http://www.helix.com", configurations, false);
        }

        void MatchConfiguredStartUrl()
        {
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://www.helix.com" }
            }));
            Add("http://www.helix.com", configurations, true);
        }

        void ThrowExceptionIfArgumentNull() { Add(null, p4: typeof(ArgumentNullException)); }
    }
}