using ComponentInterfaces;
using ComponentManagerAPI.GeneralExtensions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ComponentManagerAPI.Services.DataDisplay
{

    public class DisplayNameMetaDataProvider : IDisplayMetadataProvider
    {
        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {

            var propertyAttributes = context.Attributes;
            var modelMetadata = context.DisplayMetadata;
            var propertyName = context.Key.Name;

            if (IsTransformRequired(propertyName, modelMetadata, propertyAttributes))
            {
                modelMetadata.DisplayName = () => Regex.Replace(propertyName, "([a-z])([A-Z])", "$1 $2");
            }
        }

        private static bool IsTransformRequired(string propertyName, DisplayMetadata modelMetadata, IReadOnlyList<object> propertyAttributes)
        {
            if (!string.IsNullOrEmpty(modelMetadata.SimpleDisplayProperty))
                return false;

            if (propertyAttributes.OfType<DisplayNameAttribute>().Any())
                return false;

            //if (propertyAttributes.OfType<DisplayAttribute>().Any())
            //    return false;

            if (string.IsNullOrEmpty(propertyName))
                return false;

            return true;
        }
    }


    public class ActionModelMetadataProvider : IDisplayMetadataProvider
    {
        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {
            //get current view model type
            Type modType = context.Key.ModelType;


            //if view model is action
            if (typeof(IComponentActionStructure).IsAssignableFrom(modType))
            {
                //use action template
                context.DisplayMetadata.TemplateHint = "Action";
            }

        }
    }

    public class ComplexModelMetadataProvider : IDisplayMetadataProvider
    {
        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {
            //if template hint is null or empty
            if (string.IsNullOrEmpty(context.DisplayMetadata.TemplateHint))
            {
                //get current view model type
                Type modType = context.Key.ModelType;

                //if it is a non-primitive, non-enum, non-collection class 
                if (!typeof(IEnumerable).IsAssignableFrom(modType)
                    && !modType.IsPrimitive())
                {
                    //use complex type template
                    context.DisplayMetadata.TemplateHint = "Complex";
                }
            }
        }
    }

    public class PrimitiveModelMetadataProvider : IDisplayMetadataProvider
    {
        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {
            //if template hint is null or empty
            if (string.IsNullOrEmpty(context.DisplayMetadata.TemplateHint))
            {
                //get current view model type
                Type modType = context.Key.ModelType;

                //if it is primitive, deci, float, double, or date 
                if (modType.IsPrimitive())
                {
                    //use numeric type template
                    context.DisplayMetadata.TemplateHint = "Primitive";
                }
            }
        }
    }

    public class ComplexListModelMetadataProvider : IDisplayMetadataProvider
    {
        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {
            //if template hint is null or empty
            if (string.IsNullOrEmpty(context.DisplayMetadata.TemplateHint))
            {
                //get current view model type
                Type modType = context.Key.ModelType;

                //if is collection
                if (typeof(IEnumerable).IsAssignableFrom(modType))
                {
                    //if is gen type
                    if (modType.IsGenericType)
                    {
                        //get gen type args
                        var genTypeArgs = modType.GetGenericArguments();
                        //if has only one
                        if (genTypeArgs.Count() == 1)
                        {
                            //get single
                            var genType = genTypeArgs.Single();
                            //build Ilist of gen type
                            Type listType = typeof(IList<>).MakeGenericType(genTypeArgs);

                            //if IList of gen type arg is assignable from the supplied collection 
                            if (listType.IsAssignableFrom(modType))
                            {
                                //use complex collection template
                                context.DisplayMetadata.TemplateHint = "ComplexList";
                            }
                        }
                    }
                }
            }


            //if is non-primitive class that is a collection that isnt a string or deci
            //if (modType.IsClass
            //    && !modType.IsPrimitive
            //    && !modType.Equals(typeof(string))
            //    && !modType.Equals(typeof(decimal))
            //    && typeof(IEnumerable).IsAssignableFrom(modType))
            //{
            //    //if is generic type
            //    if (modType.IsGenericType)
            //    {
            //        //get gentype args
            //        Type[] genTypeArgs = modType.GetGenericArguments();

            //        //if has single gen type arg
            //        if (genTypeArgs.Count() == 1)
            //        {
            //            //get single gentype arg
            //            Type genTypeArg = genTypeArgs.Single();

            //            //if gentype is a string
            //            if (genTypeArg.Equals(typeof(string)))
            //            {
            //                //use string collection template
            //                context.DisplayMetadata.TemplateHint = "StringCollection";
            //            }
            //            else
            //            {
            //                //use complex collection template
            //                context.DisplayMetadata.TemplateHint = "ComplexCollection";
            //            }
            //        }
            //        else
            //        {

            //        }
            //    }
            //    //if not handled
            //    else
            //    {
            //        //use complex collection template
            //        context.DisplayMetadata.TemplateHint = "ComplexCollection";

            //    }
            //}
        }
    }

}

