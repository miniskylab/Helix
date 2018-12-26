using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Helix.Specifications.Core;
using Newtonsoft.Json;

namespace Helix.Crawler.Specifications
{
    internal class InternalResourceDefinition : TheoryDescription<Configurations, IResource, bool, Type>
    {
        public InternalResourceDefinition()
        {
            ShareHostNameWithParent();
            MatchConfiguredDomainName();
            IsStartUrlUsingDomainName();
            IsStartUrlUsingIpAddress();

            ThrowExceptionIfArgumentNull();
            ThrowExceptionIfArgumentIsNotValid();

            IsNotInternalResourceInAllOtherCases();
        }

        [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
        void IsNotInternalResourceInAllOtherCases()
        {
            var resource = ServiceLocator.Get<IResource>();
            resource.Uri = new Uri("http://www.helix.com/anything");
            resource.ParentUri = new Uri("http://192.168.1.2");
            AddTheoryDescription(p2: resource, p3: false);
        }

        void IsStartUrlUsingDomainName()
        {
            var resource = ServiceLocator.Get<IResource>();
            resource.ParentUri = null;
            resource.Uri = new Uri("http://www.helix.com");
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);
        }

        void IsStartUrlUsingIpAddress()
        {
            var resource = ServiceLocator.Get<IResource>();
            resource.ParentUri = null;
            resource.Uri = new Uri("http://www.helix.com");
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://192.168.1.2" },
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);
        }

        void MatchConfiguredDomainName()
        {
            var resource = ServiceLocator.Get<IResource>();
            resource.ParentUri = new Uri("http://192.168.1.2");
            resource.Uri = new Uri("http://www.helix.com/anything");
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);
        }

        void ShareHostNameWithParent()
        {
            var resource = ServiceLocator.Get<IResource>();
            resource.ParentUri = new Uri("http://www.helix.com");
            resource.Uri = new Uri("http://www.helix.com/anything");
            AddTheoryDescription(p2: resource, p3: true);
        }

        void ThrowExceptionIfArgumentIsNotValid()
        {
            var resource = ServiceLocator.Get<IResource>();
            resource.Uri = new Uri("http://www.helix.com");
            resource.ParentUri = null;
            AddTheoryDescription(p2: resource, p4: typeof(ArgumentException));
        }

        void ThrowExceptionIfArgumentNull()
        {
            var resource = ServiceLocator.Get<IResource>();
            resource.Uri = null;
            AddTheoryDescription(p2: resource, p4: typeof(ArgumentNullException));
            AddTheoryDescription(p2: null, p4: typeof(ArgumentNullException));
        }
    }
}