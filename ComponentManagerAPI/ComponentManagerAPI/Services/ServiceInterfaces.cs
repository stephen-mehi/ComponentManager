using ComponentInterfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ComponentManagerAPI.Services
{



    public interface IActionTransformService
    {
        IEnumerable<IComponentActionStructure> ConvertToActionStructureCollection(IEnumerable<MethodInfo> actionCollection);
    }

    public interface IExternalDependencyData
    {
        string Name { get; set; }
        string RemoteAssemblyPath { get; set; }
        string BackupAssemblyPath { get; set; }
        string ClassName { get; set; }
        string InterfaceTypeName { get; set; }
        bool IsAutoLoaded { get; set; }
        int Scope { get; set; } 
    }

    public interface IExternalTypeResolver
    {
        Type GetExternalType(string assemblyPath, string className, Type targetType = null, Type[] genTypeParams = null);
    }

    public interface IActionResultWrapperService
    {
        IActionResult GenerateOkActionResult(object resultObject, Controller controller, string viewPath = null);
        IActionResult GenerateOkActionResult();
    }

    public interface IHandleAssemblyResolving
    {
        Assembly ResolveAssembly(object sender, ResolveEventArgs args);
    }


    public interface IIoC_ComponentModelBinderProvider : IModelBinderProvider
    {

    }

}
