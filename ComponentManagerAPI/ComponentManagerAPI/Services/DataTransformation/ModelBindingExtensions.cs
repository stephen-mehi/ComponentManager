using CommonServiceInterfaces;
using ComponentInterfaces;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelBinderService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ComponentManagerAPI.Services.DataTransformation
{


    #region InterfacesBinder

    //public class InterfacesModelBinderProvider : IModelBinderProvider
    //{
    //    public IModelBinder GetBinder(ModelBinderProviderContext context)
    //    {
    //        if (context == null)
    //        {
    //            throw new ArgumentNullException(nameof(context));
    //        }

    //        if ((!context.Metadata.IsCollectionType &&
    //            (context.Metadata.ModelType.GetTypeInfo().IsInterface ||
    //             context.Metadata.ModelType.GetTypeInfo().IsAbstract) &&
    //            (context.BindingInfo.BindingSource == null ||
    //            !context.BindingInfo.BindingSource
    //            .CanAcceptDataFrom(BindingSource.Services))))
    //        {
    //            var propertyBinders = new Dictionary<ModelMetadata, IModelBinder>();
    //            for (var i = 0; i < context.Metadata.Properties.Count; i++)
    //            {
    //                var property = context.Metadata.Properties[i];
    //                propertyBinders.Add(property, context.CreateBinder(property));
    //            }
    //            return new InterfacesModelBinder(propertyBinders);
    //        }


    //        return null;
    //    }
    //}

    //public class InterfacesModelBinder : ComplexTypeModelBinder
    //{

    //    public InterfacesModelBinder(IDictionary<ModelMetadata, IModelBinder> propertyBinder)
    //        : base(propertyBinder)
    //    {

    //    }
    //    protected override object CreateModel(ModelBindingContext bindingContext)
    //    {
    //        var service = bindingContext.HttpContext.RequestServices.GetService(bindingContext.ModelType);
    //        return service;
    //    }
    //}

    #endregion


    #region IoC_ComponentDataModelBinder


    public class RuntimeComponentModelBinderProvider : IIoC_ComponentModelBinderProvider
    {

        public RuntimeComponentModelBinderProvider(
            IServiceProvider sp,
            IList<IInputFormatter> inputFormatters)
        {
            RequestReaderFactory = sp.GetRequiredService<IHttpRequestStreamReaderFactory>();
            InputFormatters = inputFormatters;
        }

        private readonly IList<IInputFormatter> InputFormatters;
        private readonly IHttpRequestStreamReaderFactory RequestReaderFactory;


        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {

            if (context == null)
                throw new ArgumentNullException("Failed to get binder. Context obj cannot be null");


            //if the model type needs an injected concrete type, use runtime binder,
            //else just use built in complextype binder
            //If the model type is: 
            //1. not a collection
            //2. an interface or an abstract class
            //3. the binding source is null, or if it has one, can't accept from services(the "from services" attr has already been applied)
            if ((!context.Metadata.IsCollectionType &&
                (context.Metadata.ModelType.GetTypeInfo().IsInterface ||
                 context.Metadata.ModelType.GetTypeInfo().IsAbstract) &&
                (context.BindingInfo.BindingSource == null ||
                !context.BindingInfo.BindingSource
                .CanAcceptDataFrom(BindingSource.Services))))
            {
                var propBinders = context.Metadata.Properties.ToDictionary(p => p, p => context.CreateBinder(p));

                RuntimeComponentModelBinder compBinder = new RuntimeComponentModelBinder(
                    new BodyModelBinder(InputFormatters, RequestReaderFactory),
                    new ComplexRuntimeTypeModelBinder(propBinders, context),
                    context);

                return compBinder;
            }

            //else return null
            return null;
        }
    }


    public class RuntimeComponentModelBinder : IModelBinder
    {

        public RuntimeComponentModelBinder(
            IModelBinder defaultBodyBinder,
            IModelBinder defaultComplexModelBinder,
            ModelBinderProviderContext modelBinderProviderContext)
        {
            DefaultBodyBinder = defaultBodyBinder;
            DefaultComplexModelBinder = defaultComplexModelBinder;
            ModelBinderProviderContext = modelBinderProviderContext;
        }

        private IModelBinder DefaultBodyBinder;
        private IModelBinder DefaultComplexModelBinder;
        private ModelBinderProviderContext ModelBinderProviderContext;

        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            string failurePrefix = "Failed to model bind worklist generator plugin. ";

            var serviceProvDep = bindingContext.HttpContext.RequestServices;
            var _genericFactoryDependency = (IGenericInjectionFactory)serviceProvDep.GetService(typeof(IGenericInjectionFactory));

            //if context null
            if (bindingContext == null)
                throw new ArgumentNullException(failurePrefix + "Binding context cannot be null");

            //if not model is set for current binding context
            if (bindingContext.Model == null)
            {
                //if Icomponent adapter model type, and is the top level object
                if (typeof(IComponentAdapter).IsAssignableFrom(bindingContext.ModelType) && bindingContext.IsTopLevelObject)
                {

                    //init assembly path. Get posted assembly path key with full model prefix
                    string assemblyPathKey = nameof(IComponentConstructionData.AssemblyPath);
                    //init class name. Get posted class name key with full model prefix
                    string classNameKey = nameof(IComponentConstructionData.ClassName);

                    //try get assembly path value
                    ValueProviderResult assemblyPathResult = bindingContext.ValueProvider.GetValue(assemblyPathKey);
                    //try get class name value
                    ValueProviderResult classNameResult = bindingContext.ValueProvider.GetValue(classNameKey);
                    //ensure class and assembly path found
                    if (assemblyPathResult == ValueProviderResult.None || classNameResult == ValueProviderResult.None)
                        throw new ArgumentNullException(failurePrefix + "Assembly path or class name not found");

                    //create instance of component
                    IComponentAdapter component =
                        (IComponentAdapter)_genericFactoryDependency
                        .Construct(assemblyPath: assemblyPathResult.FirstValue, className: classNameResult.FirstValue, serviceProviderDep: serviceProvDep, targetType: typeof(IComponentAdapter));

                    //set model
                    bindingContext.Model = component ?? throw new TypeLoadException(failurePrefix + "Loaded plugin was null");
                }
                //else 
                else
                {
                    //try to set model from services
                    bindingContext.Model = bindingContext.HttpContext.RequestServices.GetService(bindingContext.ModelType);
                }
            }


            //request data is form
            bool isForm = bindingContext.HttpContext.Request.HasFormContentType;

            //if is form request
            if (isForm)
            {
                //use dynamic complex binder
                await DefaultComplexModelBinder.BindModelAsync(bindingContext);
            }
            else
            {
                await DefaultBodyBinder.BindModelAsync(bindingContext);
            }

        }
    }


    #endregion


    //COMMENTED BECAUSE RUNTIME SHOULD NOT BE USING THIS BINDER
    //WE SHOULD ONLY ACCESS THIS BINDER FROM THE RUNTIME COMPOENNT BINDER
    /// <summary>
    /// An <see cref="IModelBinderProvider"/> for complex runtime types.
    /// </summary>
    //public class ComplexRuntimeTypeModelBinderProvider : IModelBinderProvider
    //{
    //    /// <inheritdoc />
    //    public IModelBinder GetBinder(ModelBinderProviderContext context)
    //    {
    //        if (context == null)
    //        {
    //            throw new ArgumentNullException(nameof(context));
    //        }

    //        //if the model type needs an injected concrete type, use runtime binder,
    //        //else just use built in complextype binder
    //        if ((!context.Metadata.IsCollectionType &&
    //            (context.Metadata.ModelType.GetTypeInfo().IsInterface ||
    //             context.Metadata.ModelType.GetTypeInfo().IsAbstract) &&
    //            (context.BindingInfo.BindingSource == null ||
    //            !context.BindingInfo.BindingSource
    //            .CanAcceptDataFrom(BindingSource.Services))))
    //        {
    //            var propertyBinders = new Dictionary<ModelMetadata, IModelBinder>();
    //            foreach (var property in context.Metadata.Properties)
    //            {
    //                propertyBinders.Add(property, context.CreateBinder(property));
    //            }

    //            return new ComplexRuntimeTypeModelBinder(propertyBinders, context);
    //        }

    //        return null;
    //    }
    //}

}



