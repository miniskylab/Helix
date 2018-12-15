using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Helix.Specifications.Core;
using Newtonsoft.Json;

namespace Helix.Crawler.Specifications
{
    class InternalResourceDefinition : TheoryData<IResource, Configurations, bool, Type>
    {
        [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
        public InternalResourceDefinition()
        {
            var resource = ServiceLocator.Get<IResource>();
            resource.Uri = new Uri("http://www.helix.com/anything");
            resource.ParentUri = new Uri("http://www.helix.com");
            Add(resource, p3: true);

            resource = ServiceLocator.Get<IResource>();
            resource.Uri = new Uri("http://www.helix.com/anything");
            resource.ParentUri = new Uri("http://192.168.1.2:8080");
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            Add(resource, configurations, true);
            
            resource = ServiceLocator.Get<IResource>();
            resource.Uri = new Uri("http://www.helix.com");
            resource.ParentUri = null;
            configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://www.helix.com" }
            }));
            Add(resource, configurations, true);
            
            resource = ServiceLocator.Get<IResource>();
            resource.Uri = new Uri("http://www.helix.com");
            resource.ParentUri = null;
            configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://192.168.1.2:8080" },
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            Add(resource, configurations, true);

            Add(null, p4: typeof(ArgumentNullException));
            
            resource = ServiceLocator.Get<IResource>();
            resource.Uri = null;
            Add(resource, p4: typeof(ArgumentNullException));
            
            resource = ServiceLocator.Get<IResource>();
            resource.Uri = new Uri("http://www.helix.com");
            resource.ParentUri = null;
            Add(resource, p4: typeof(InvalidDataException));
            
            resource = ServiceLocator.Get<IResource>();
            resource.Uri = new Uri("http://www.helix.com/anything");
            resource.ParentUri = new Uri("http://192.168.1.2:8080");
            Add(resource, p3: false);
        }
    }
}