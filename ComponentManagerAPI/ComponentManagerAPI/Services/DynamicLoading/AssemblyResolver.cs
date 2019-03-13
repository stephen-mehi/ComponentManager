using ComponentInterfaces;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ComponentManagerAPI.Services.DynamicLoading
{
    public class AssemblyResolver : IHandleAssemblyResolving
    {

        private readonly IAssemblyProbing _asmProbingDependency;

        public AssemblyResolver(
            IAssemblyProbing asmProbingDependency)
        {
            _asmProbingDependency = asmProbingDependency;
        }



        private Assembly MyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            //This handler is called only when the common language runtime tries to bind to the assembly and fails.

            //Retrieve the list of referenced assemblies in an array of AssemblyName.
            Assembly MyAssembly;
            Assembly objExecutingAssemblies;
            string strTempAssmbPath = "";

            objExecutingAssemblies = Assembly.GetExecutingAssembly();
            AssemblyName[] arrReferencedAssmbNames = objExecutingAssemblies.GetReferencedAssemblies();

            //Loop through the array of referenced assembly names.
            foreach (AssemblyName strAssmbName in arrReferencedAssmbNames)
            {
                //Check for the assembly names that have raised the "AssemblyResolve" event.
                if (strAssmbName.FullName.Substring(0, strAssmbName.FullName.IndexOf(",")) == args.Name.Substring(0, args.Name.IndexOf(",")))
                {
                    //Build the path of the assembly from where it has to be loaded.
                    strTempAssmbPath = "C:\\Myassemblies\\" + args.Name.Substring(0, args.Name.IndexOf(",")) + ".dll";
                    break;
                }

            }
            //Load the assembly from the specified path. 
            MyAssembly = Assembly.LoadFrom(strTempAssmbPath);

            //Return the loaded assembly.
            return MyAssembly;
        }


        public Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {

            //Ignore missing resources
            if (args.Name.Contains(".resources"))
            {
                return null;
            }

            //check for assemblies already loaded
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);

            //if not null
            if (assembly != null)
            {
                //return the already loaded assembly
                return assembly;
            }

            //split out the filename of the full assembly name and append the base path of the executing assembly
            string filename = args.Name.Split(',')[0] + ".dll".ToLower();

            //get late bound access to current assembly probing path. Set in controller per request
            //may be null if not in request scope
            string currentProbingPath = _asmProbingDependency.CurrentProbingPath;
            //get known fallback/resources path
            string[] privateBinFolderName = _asmProbingDependency.PrivateBinFolderName.Split(new char[] { ';' });

            //foreach bin path specified
            foreach (var path in privateBinFolderName)
            {
                //build paths for finding assembly
                string asmBinPath = Path.Combine(@".\", path, filename);

                //try to load from location
                try
                {
                    return Assembly.LoadFrom(asmBinPath);
                }
                catch (Exception)
                {
                    //swallow exception
                }
            }

            //if still looking
            //if current probing path set
            if (!string.IsNullOrEmpty(currentProbingPath))
            {
                //attempt loading from current probing path
                try
                {
                    string asmProbingPath = Path.Combine(currentProbingPath, filename);
                    return Assembly.LoadFrom(asmProbingPath);
                }
                //swallow any excptions and return null
                catch (Exception)
                {
                    return null;
                }
            }

            //return null if no probing path
            return null;

        }
    }


    public class AssemblyProbing : IAssemblyProbing
    {

        public string CurrentProbingPath { get; set; }
        public string PrivateBinFolderName { get; set; }
    }
}
