using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
                var pathToEntryAssembly = Assembly.GetEntryAssembly()?.Location;
                if (pathToEntryAssembly == null) throw new DllNotFoundException("Entry assembly not found.");

                var workingDirectory = Path.GetDirectoryName(pathToEntryAssembly);
                if (workingDirectory == null)
                    throw new DirectoryNotFoundException($"Could not obtain parent directory of: {pathToEntryAssembly}");

                var loadedAssemblies = new List<Assembly>();
                foreach (var pathToAssemblyFile in Directory.GetFiles(workingDirectory, "*.dll", SearchOption.AllDirectories))
                {
                    try { loadedAssemblies.Add(Assembly.LoadFrom(pathToAssemblyFile)); }
                    catch (FileLoadException) { }
                    catch (BadImageFormatException) { }
                }

                var registrableTypes = new List<Type>();
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

                    if (type.IsGenericType) containerBuilder.RegisterGeneric(type).As(matchingInterfaceType);
                    else containerBuilder.RegisterType(type).As(matchingInterfaceType);
                }
            }

            #endregion
        }
    }
}