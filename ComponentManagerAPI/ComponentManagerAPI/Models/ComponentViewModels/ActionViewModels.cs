using ComponentInterfaces;
using System;
using System.Collections.Generic;

namespace ComponentManagerAPI.Models.ComponentViewModels
{
    public class ComponentActionStructure : IComponentActionStructure
    {

        public ComponentActionStructure()
        {
            Parameters = new List<IComponentActionParameterStructure>();
        }


        public IList<IComponentActionParameterStructure> Parameters { get; set; }
        public string MemberName { get; set; }
        public string MemberID { get; set; }
        public string MemberAlias { get; set; }
        public string MemberDescription { get; set; }
        public string ActionURL { get; set; }
        public bool RequiresInitialize { get; set; }
        //public bool UseCustomUi { get; set; }
        //public string CustomUiUri { get; set; }
        public bool IsIndependent { get; set; }
    }

    public class ComponentActionParameterStructure : IComponentActionParameterStructure
    {
        public string MemberName { get; set; }
        public string MemberID { get; set; }
        public string MemberAlias { get; set; }
        public string MemberDescription { get; set; }
        public string ParameterValue { get; set; }
        public Type ParameterType { get; set; }
    }
}
