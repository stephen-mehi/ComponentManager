using System;
using System.Linq;
using System.Reflection;

namespace ComponentManagerAPI.Services.DynamicLoading
{
    public class ExternalTypeResolver : IExternalTypeResolver
    {

        public Type GetExternalType(string assemblyPath, string className, Type targetType = null, Type[] genTypeParams = null)
        {

            //check that assembly and class name are supplied
            if (string.IsNullOrEmpty(assemblyPath) || string.IsNullOrEmpty(className))
            {
                throw new ArgumentNullException("Missing assembly path or class name");
            }

            //load assembly dynamically
            Assembly assembly = Assembly.LoadFrom(assemblyPath);

            //ensure asm loaded ok
            if (assembly == null)
                throw new ApplicationException(string.Format("Could not load assembly: {0}", assemblyPath));

            //get type from assembly
            Type typeObject = assembly.GetTypes()?.FirstOrDefault(t => t.Name.Equals(className, StringComparison.Ordinal) || t.FullName.Equals(className, StringComparison.Ordinal));

            //ensure non-null activation
            if (typeObject == null)
                throw new ApplicationException(string.Format("Could not find type in assembly: {0} class name: {1}", assemblyPath, className));

            //if gen type def
            if (typeObject.IsGenericTypeDefinition)
            {
                //if gen type params supplied
                if (genTypeParams != null && genTypeParams.Length > 0)
                    typeObject = typeObject.MakeGenericType(genTypeParams);//create closed generic type

            }

            ////ensure type is, derives from, or implements expected type, or is null and we dont care
            //if (targetType != null && !targetType.IsAssignableFrom(typeObject))
            //    throw new TypeLoadException(string.Format("Resolved type is not the specified target type. Target type: {0} Resolved type: {1}", targetType, typeObject));

            return typeObject;

        }
    }
}
