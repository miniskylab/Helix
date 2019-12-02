using System;
using System.Collections.Generic;
using Helix.Bot.Abstractions;
using Helix.Specifications;
using Newtonsoft.Json;

namespace Helix.Bot.Specifications
{
    internal class InternalResourceDescription : TheoryDescription<Configurations, Resource, bool, Type>
    {
        public InternalResourceDescription()
        {
            ShareHostNameWithParent();
            MatchConfiguredRemoteHost();
            IsStartUriByHostName();
            IsStartUriByIpAddress();

            IsNotInternalResourceInAllOtherCases();

            ThrowExceptionIfArgumentNull();
            ThrowExceptionIfArgumentIsNotValid();
        }

        void IsNotInternalResourceInAllOtherCases()
        {
            AddTheoryDescription(p2: new Resource(0, "http://www.sanity.com/anything", new Uri("http://www.helix.com"), false));
        }

        void IsStartUriByHostName()
        {
            var resource = new Resource(0, "http://www.helix.com", null, false);
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);
        }

        void IsStartUriByIpAddress()
        {
            var resource = new Resource(0, "http://www.helix.com", null, false);
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://192.168.1.2" },
                { nameof(Configurations.RemoteHost), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);
        }

        void MatchConfiguredRemoteHost()
        {
            var resource = new Resource(0, "http://www.helix.com/child", new Uri("http://192.168.1.2/parent"), false);
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://192.168.1.2" },
                { nameof(Configurations.RemoteHost), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);

            resource = new Resource(0, "http://www.helix.com/child", new Uri("http://192.168.1.2/parent"), false);
            configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://192.168.1.2" },
                { nameof(Configurations.RemoteHost), "helix.com" }
            }));
            AddTheoryDescription(configurations, resource);
        }

        void ShareHostNameWithParent()
        {
            var resource = new Resource(0, "http://www.helix.com/child", new Uri("http://www.helix.com/parent"), false);
            AddTheoryDescription(p2: resource, p3: true);
        }

        void ThrowExceptionIfArgumentIsNotValid()
        {
            var resource = new Resource(0, "http://www.helix.com", null, false);
            AddTheoryDescription(p2: resource, p4: typeof(ArgumentException));
        }

        void ThrowExceptionIfArgumentNull()
        {
            var resource = new Resource(0, "http://anything.com", null, false) { Uri = null };
            AddTheoryDescription(p2: resource, p4: typeof(ArgumentNullException));
            AddTheoryDescription(p2: null, p4: typeof(ArgumentNullException));
        }
    }
}