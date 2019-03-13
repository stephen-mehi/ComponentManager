using System;

namespace ComponentManagerAPI.Services.DynamicLoading
{
    public class ExternalDependencyModel : IExternalDependencyData
    {
        public string Name { get; set; }
        public string RemoteAssemblyPath { get; set; }
        public string BackupAssemblyPath { get; set; }
        public string ClassName { get; set; }
        public string InterfaceTypeName { get; set; }
        public bool IsAutoLoaded { get; set; }
        public int Scope { get; set; }
    }
}
