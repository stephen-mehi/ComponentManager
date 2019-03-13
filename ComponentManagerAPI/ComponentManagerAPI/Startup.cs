using CommonServiceInterfaces;
using ComponentInterfaces;
using ComponentManagerAPI.Middleware;
using ComponentManagerAPI.Models.ComponentViewModels;
using ComponentManagerAPI.Services;
using ComponentManagerAPI.Services.DataDisplay;
using ComponentManagerAPI.Services.DataTransformation;
using ComponentManagerAPI.Services.DynamicLoading;
using ComponentManagerAPI.Services.ErrorHandling;
using ComponentManagerAPI.Services.ServiceBuilderExtensions;
using ComponentManagerService;
using EmailService;
using GenericFactoryServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SerializationServices;
using System;
using System.Collections.Generic;
using System.Linq;
using TypeManipulationServices;

namespace ComponentManagerAPI
{
    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //REMOVAL **Dependencies are bundled with assembly at compile instead of loading them at runtime to avoid runtime failures and delicate deployments

            //IEnumerable<IExternalDependencyData> externalDependencies = new List<ExternalDependencyModel>();
            ////get all extern dep from config
            //Configuration.Bind(key:"ExternalDependencies", instance: externalDependencies);
            ////get ctor collection dependency
            //IExternalDependencyData ctorCollectionDep = externalDependencies.Single(d => !d.IsAutoLoaded && d.Name.Equals("ComponentConstructionCollection", StringComparison.Ordinal));
            ////get ctor element dependency
            //IExternalDependencyData elementDependency = externalDependencies.Single(d => d.Name.Equals("ConstructionData", StringComparison.Ordinal));

            ////get list of external dependencies from config file
            //var autoLoadedExternalDependencies = externalDependencies.Where(e => e.IsAutoLoaded);


            //REGISTER ALL SERVICED(DEPENDENCIES)
            services
                .AddSingleton(Configuration)
                .AddScoped<ICodeContractService, CodeContract>()
                //add scoped assembly probing location
                .AddScoped(typeof(IAssemblyProbing), sp =>
                {
                    var asmProbe = new AssemblyProbing();
                    asmProbe.PrivateBinFolderName = Configuration["PrivateBinFolderName"];
                    return asmProbe;
                })
                //add asm res
                .AddTransient(typeof(IHandleAssemblyResolving), sp =>
                {
                    IAssemblyProbing asmProbe = sp.GetRequiredService<IAssemblyProbing>();
                    IHandleAssemblyResolving asmRes = new AssemblyResolver(asmProbe);

                    return asmRes;
                })
                //add config to services
                .AddSingleton(Configuration)
                //add factory constructor service
                .AddSingleton<IExternalTypeResolver, ExternalTypeResolver>()
                //add all external dependencies from config file
                //.AddAutoExternalDependencies(autoLoadedExternalDependencies)
                //*****Add common services
                .AddScoped<IEmailMessenger, EmailMessengerService>()
                .AddScoped<IGenericFactory, GenericFactoryService>()
                .AddScoped<IGenericInjectionFactory, GenericInjectionFactoryService>()
                .AddScoped<IComponentSerializer, PolymorphicXmlSerializer>()
                .AddScoped<IComponentPersistence, ComponentStatePersistenceService>()
                .AddScoped<ITypeManipulator, TypeManipulationService>()
                //*****Add component manager types
                .AddScoped<IComponentConstructionData, ComponentConstructionData>()
                .AddScoped(typeof(IComponentDataModel<,>), typeof(ComponentDataModel<,>))
                .AddScoped(typeof(IComponentCollection<IComponentConstructionData>), typeof(ComponentConstructionCollection<ComponentConstructionData>))
                .AddScoped(typeof(IComponentManager<IComponentAdapter, IComponentConstructionData>), typeof(ComponentManager))
                .AddScoped<IComponentEqualityComparer, ComponentConstructionDataComparer>()
                //add IoC type model binder provider
                .AddScoped<IIoC_ComponentModelBinderProvider, RuntimeComponentModelBinderProvider>()
                //add action transform service
                .AddSingleton<IActionTransformService, ActionTransform>()
                //add action param marker attribute
                .AddTransient<IComponentActionStructure, ComponentActionStructure>()
                //add action param marker attribute
                .AddTransient<IComponentActionParameterStructure, ComponentActionParameterStructure>()
                //action filter dependency
                .AddScoped<IActionResultWrapperService, ActionResultWrapper>()
                //add exception filter dep
                .AddScoped<IExceptionFilter, ApiExceptionFilter>()
                //add ctor collections
                //.AddComponentCollectionDependency(elementDependency, ctorCollectionDep)
                //register mvc services
                .AddMvc()
                .AddXmlSerializerFormatters()
                .AddMvcOptions(options =>
                {

                    //add custom model binders 
                    //config.ModelBinderProviders.Insert(0, new ComponentModelBinderProvider());
                    //add custom xml input/output formatters
                    //config.InputFormatters.Insert(0, new IComponentXmlSerializerInputFormatter());
                    //config.OutputFormatters.Insert(0, new IComponentXmlSerializerOutputFormatter());

                    //add XML Content Negotiation
                    options.RespectBrowserAcceptHeader = true;
                    IServiceProvider intermediateProvider = services.BuildServiceProvider();
                    IIoC_ComponentModelBinderProvider compBinderProvider = new RuntimeComponentModelBinderProvider(intermediateProvider, options.InputFormatters);

                    //options.ModelBinderProviders.Remove(options.ModelBinderProviders.FirstOrDefault(m => typeof(ComplexTypeModelBinderProvider).Equals(m.GetType())));
                    //options.ModelBinderProviders.Insert(0, new ComplexRuntimeTypeModelBinderProvider());

                    options.ModelBinderProviders.Insert(0, compBinderProvider);

                    options.ModelMetadataDetailsProviders.Add(new DisplayNameMetaDataProvider());
                    options.ModelMetadataDetailsProviders.Add(new ActionModelMetadataProvider());
                    options.ModelMetadataDetailsProviders.Add(new ComplexModelMetadataProvider());
                    options.ModelMetadataDetailsProviders.Add(new ComplexListModelMetadataProvider());
                    options.ModelMetadataDetailsProviders.Add(new PrimitiveModelMetadataProvider());

                })
                .AddJsonOptions(options =>
                {

                    //add custom json converters
                    //options.SerializerSettings.Converters.Add(new ComponentJsonConverter());
                    options.SerializerSettings.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All;
                    options.SerializerSettings.TypeNameAssemblyFormatHandling = Newtonsoft.Json.TypeNameAssemblyFormatHandling.Full;
                    options.SerializerSettings.Error += (sender, args) =>
                    {
                        Console.WriteLine(args.ErrorContext.Error.Message);
                        args.ErrorContext.Handled = true;
                    };
                });


            ////add http context accessor
            //.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            //iis config
            services.Configure<IISOptions>(options =>
            {
                options.AutomaticAuthentication = true;
                options.ForwardClientCertificate = true;
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IServiceProvider sp)
        {

            //hook up assembly resolve handler before subsequent services are rendered
            //since the runtime may need some help resolving some assemblies below
            AppDomain.CurrentDomain.AssemblyResolve += sp.GetRequiredService<IHandleAssemblyResolving>().ResolveAssembly;

            app.UseBrowserLink();

            app.UseDeveloperExceptionPage();


            //if (env.IsDevelopment())
            //{
            //    app.UseDeveloperExceptionPage();
            //    app.UseBrowserLink();
            //}
            //else
            //{
            //app.UseExceptionHandler("/Home/Error");
            //}

            app.UseCustomAuthorization();

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");


            });



        }
    }
}
