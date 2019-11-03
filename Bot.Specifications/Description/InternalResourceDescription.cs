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
            var resource = new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.sanity.com/anything") };
            AddTheoryDescription(p2: resource);
        }

        void IsStartUriByHostName()
        {
            var resource = new Resource { ParentUri = null, Uri = new Uri("http://www.helix.com") };
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);
        }

        void IsStartUriByIpAddress()
        {
            var resource = new Resource { ParentUri = null, Uri = new Uri("http://www.helix.com") };
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://192.168.1.2" },
                { nameof(Configurations.RemoteHost), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);
        }

        void MatchConfiguredRemoteHost()
        {
            var resource = new Resource { ParentUri = new Uri("http://192.168.1.2/parent"), Uri = new Uri("http://www.helix.com/child") };
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://192.168.1.2" },
                { nameof(Configurations.RemoteHost), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);

            resource = new Resource { ParentUri = new Uri("http://192.168.1.2/parent"), Uri = new Uri("http://www.helix.com/child") };
            configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUri), "http://192.168.1.2" },
                { nameof(Configurations.RemoteHost), "helix.com" }
            }));
            AddTheoryDescription(configurations, resource);
        }

        void ShareHostNameWithParent()
        {
            var resource = new Resource { ParentUri = new Uri("http://www.helix.com/parent"), Uri = new Uri("http://www.helix.com/child") };
            AddTheoryDescription(p2: resource, p3: true);
        }

        void ThrowExceptionIfArgumentIsNotValid()
        {
            var resource = new Resource { Uri = new Uri("http://www.helix.com"), ParentUri = null };
            AddTheoryDescription(p2: resource, p4: typeof(ArgumentException));
        }

        void ThrowExceptionIfArgumentNull()
        {
            var resource = new Resource { Uri = null };
            AddTheoryDescription(p2: resource, p4: typeof(ArgumentNullException));
            AddTheoryDescription(p2: null, p4: typeof(ArgumentNullException));
        }
    }
}