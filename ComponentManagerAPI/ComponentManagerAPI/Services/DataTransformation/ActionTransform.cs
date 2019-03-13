using ComponentInterfaces;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace ComponentManagerAPI.Services.DataTransformation
{
    public class ActionTransform : IActionTransformService
    {

        public ActionTransform(IServiceProvider serviceProvider)
        {
            _serviceProviderDependency = serviceProvider;
        }

        private IServiceProvider _serviceProviderDependency;

        public IEnumerable<IComponentActionStructure> ConvertToActionStructureCollection(IEnumerable<MethodInfo> methods)
        {

            IEnumerable<IComponentActionStructure> actionStructures =
                methods.Select((m) =>
                {
                    var actionStruct = _serviceProviderDependency.GetRequiredService<IComponentActionStructure>();
                    //var actionAttr = m.GetCustomAttribute<ComponentActionAttribute>();
                    var actionAttr = m.GetCustomAttributes()?.SingleOrDefault(a => typeof(IComponentActionAttribute).IsAssignableFrom(a.GetType())) as IComponentActionAttribute;
                    actionStruct.MemberName = m.Name;

                    if (actionAttr != null)
                    {
                        actionStruct.MemberID = actionAttr.MemberID;
                        actionStruct.MemberAlias = actionAttr.MemberAlias;
                        actionStruct.MemberDescription = actionAttr.MemberDescription;
                        actionStruct.IsIndependent = actionAttr.IsIndependent;
                    }

                    actionStruct.Parameters =
                    m.GetParameters()?
                    .Select((p) =>
                    {

                        var paramStruct = _serviceProviderDependency.GetRequiredService<IComponentActionParameterStructure>();
                        var paramAttr = p.GetCustomAttributes().SingleOrDefault(a => typeof(IComponentActionParameterAttribute).IsAssignableFrom(a.GetType())) as IComponentActionParameterAttribute;
                        //var paramAttr = p.GetCustomAttribute<ComponentActionParameterAttribute>();
                        paramStruct.MemberName = p.Name;
                        //paramStruct.ParameterValue = Activator.CreateInstance(p.ParameterType).ToString();
                        paramStruct.ParameterType = p.ParameterType;

                        if (paramAttr != null)
                        {
                            paramStruct.MemberID = paramAttr.MemberID;
                            paramStruct.MemberAlias = paramAttr.MemberAlias;
                            paramStruct.MemberDescription = paramAttr.MemberDescription;
                        }

                        return paramStruct;

                    }).ToList();

                    return actionStruct;

                });

            return actionStructures;

        }

    }

}
