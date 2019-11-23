using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;

namespace Helix.Core
{
    public static class DependencyInjection
    {
        public static ContainerBuilder GetDefaultContainerBuilder()
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterModule<Log4NetModule>();
            RegisterTransientServicesByConvention();
            return containerBuilder;

            #region Local Functions

            void RegisterTransientServicesByConvention()
            {
                var registrableTypes = new List<Type>();
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                var registrable = new Func<Type, bool>(
                    type => type.IsClass &&
                            type.IsAssignableTo<IService>() &&
                            !type.IsAbstract &&
                            !type.IsNested &&
                            !type.IsCompilerGenerated()
                );

                foreach (var assembly in loadedAssemblies) registrableTypes.AddRange(assembly.GetTypes().Where(registrable));
                foreach (var type in registrableTypes)
                {
                    var matchingInterfaceType = type.GetInterface($"I{type.Name}");
                    if (matchingInterfaceType == null) continue;
                    containerBuilder.RegisterType(type).As(matchingInterfaceType);
                }
            }

            #endregion
        }
    }
}