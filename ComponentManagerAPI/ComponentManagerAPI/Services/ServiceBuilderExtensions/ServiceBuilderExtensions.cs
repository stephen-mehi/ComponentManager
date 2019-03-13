using ComponentInterfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommonServiceInterfaces;

namespace ComponentManagerAPI.Services.ServiceBuilderExtensions
{
    public static class ServiceBuilderExtensions
    {

        private static void ValidateExternalDependency(IExternalDependencyData dependency)
        {
            if (dependency == null)
                throw new ArgumentNullException("Unable to load dependency. Cannot be null");
            if (string.IsNullOrEmpty(dependency.RemoteAssemblyPath))
                throw new ArgumentNullException("Unable to load assembly: " + dependency.Name + " because remote path not set");
            if (string.IsNullOrEmpty(dependency.ClassName))
                throw new ArgumentNullException("Unable to load assembly: " + dependency.Name + " because class name not set");
            if (string.IsNullOrEmpty(dependency.InterfaceTypeName))
                throw new ArgumentNullException("Unable to load assembly: " + dependency.Name + " because interface type not set");
        }


        public static IServiceCollection AddComponentCollectionDependency(
        this IServiceCollection services,
        IExternalDependencyData elementDependency,
        IExternalDependencyData collectionDependency)
        {

            ValidateExternalDependency(elementDependency);
            ValidateExternalDependency(collectionDependency);

            //get the interface through which the application operates on the collection dependency
            Type collectionInterfaceType = Type.GetType(collectionDependency.InterfaceTypeName, throwOnError: true);
            //get the interface through which the application operates on the collection element dep
            Type elementInterfaceType = Type.GetType(elementDependency.InterfaceTypeName, throwOnError: true);
            //get scope
            ServiceLifetime scope = (ServiceLifetime)elementDependency.Scope;

            IServiceProvider sp = services.BuildServiceProvider();

            //get gen factory 
            IExternalTypeResolver typeResDep = sp.GetRequiredService<IExternalTypeResolver>();
            //get asm probe service 
            IAssemblyProbing asmProbe = sp.GetRequiredService<IAssemblyProbing>();

            //set current probing path if not null
            asmProbe.CurrentProbingPath = Path.GetDirectoryName(elementDependency.RemoteAssemblyPath);

            //declare concrete collection type
            Type collectionDependencyType = null;

            try
            {
                //get concrete collection type 
                collectionDependencyType = typeResDep.GetExternalType(collectionDependency.RemoteAssemblyPath, collectionDependency.ClassName, collectionInterfaceType);
            }
            catch (Exception)
            {
                //swallow exception
                //if back up path null
                if (string.IsNullOrEmpty(collectionDependency.BackupAssemblyPath))
                    throw;//rethrow

                //get concrete collection type from backup
                collectionDependencyType = typeResDep.GetExternalType(collectionDependency.BackupAssemblyPath, collectionDependency.ClassName, collectionInterfaceType);
                //set current probing path
                asmProbe.CurrentProbingPath = Path.GetDirectoryName(elementDependency.BackupAssemblyPath);
            }

            //get concrete element type
            Type elementDepType = sp.GetRequiredService(elementInterfaceType).GetType();

            //if not gen type def
            if (!collectionDependencyType.IsGenericTypeDefinition)
            {
                //if not gen type either
                if (!collectionDependencyType.IsGenericType)
                    throw new ArgumentException("");//throw 

                //get gen type def
                collectionDependencyType = collectionDependencyType.GetGenericTypeDefinition();
            }


            //construct gen type based on concrete element type
            Type genType = collectionDependencyType.MakeGenericType(elementDepType);


            return services.RegisterScopedDependencyHelper(scope, collectionInterfaceType, genType);

        }


        public static IServiceCollection AddAutoExternalDependencies(
            this IServiceCollection services,
            IEnumerable<IExternalDependencyData> externalDependencies)
        {
            //get only the externs that should be 
            var autoLoadedExternalDependencies = externalDependencies.Where(e => e.IsAutoLoaded);
            //build temp service provider
            var sp = services.BuildServiceProvider();

            foreach (var externDep in autoLoadedExternalDependencies)
            {
                RegisterExternalDependency(services, sp, externDep);
            }

            return services;
        }


        public static IServiceCollection RegisterExternalDependency(
        this IServiceCollection services,
        IServiceProvider sp,
        IExternalDependencyData externDep)
        {
            ValidateExternalDependency(externDep);

            //get gen factory 
            IExternalTypeResolver typeResDep = sp.GetRequiredService<IExternalTypeResolver>();
            //get asm probe service 
            IAssemblyProbing asmProbe = sp.GetRequiredService<IAssemblyProbing>();

            //set current probing path if not null
            asmProbe.CurrentProbingPath = Path.GetDirectoryName(externDep.RemoteAssemblyPath);

            //get type
            Type dependencyType = null; 

            try
            {
                //try to load from remote path
                dependencyType = typeResDep.GetExternalType(externDep.RemoteAssemblyPath, externDep.ClassName, Type.GetType(externDep.InterfaceTypeName, throwOnError: true));
            }
            catch (Exception)
            {
                //swallow exception
                //if back null
                if (string.IsNullOrEmpty(externDep.BackupAssemblyPath))
                    throw;//rethrow

                //try to load from remote path
                dependencyType = typeResDep.GetExternalType(externDep.BackupAssemblyPath, externDep.ClassName, Type.GetType(externDep.InterfaceTypeName, throwOnError: true));
                //set current probing path
                asmProbe.CurrentProbingPath = Path.GetDirectoryName(externDep.BackupAssemblyPath);
            }

            //get the interface through which the application operates on the dependency
            Type dependencyInterfaceType = Type.GetType(externDep.InterfaceTypeName, throwOnError: true);
            //get scope
            ServiceLifetime scope = (ServiceLifetime)externDep.Scope;

            services.RegisterScopedDependencyHelper(scope, dependencyInterfaceType, dependencyType);

            return services;


        }



        public static IServiceCollection RegisterScopedDependencyHelper(
            this IServiceCollection services,
            ServiceLifetime scope,
            Type dependencyInterfaceType,
            Type dependencyConcreteType)
        {
            //register the service as requested
            if (scope == ServiceLifetime.Scoped)
            {
                services.AddScoped(dependencyInterfaceType, dependencyConcreteType);
            }
            else if (scope == ServiceLifetime.Singleton)
            {
                services.AddSingleton(dependencyInterfaceType, dependencyConcreteType);
            }
            else if (scope == ServiceLifetime.Transient)
            {
                services.AddTransient(dependencyInterfaceType, dependencyConcreteType);
            }
            else
            {
                throw new ArgumentException("scope not specified for dynamically loaded type");
            }

            return services;

        }


    }
}
