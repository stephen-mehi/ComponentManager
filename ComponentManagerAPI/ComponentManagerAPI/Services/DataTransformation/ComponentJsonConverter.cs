using CommonServiceInterfaces;
using ComponentInterfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace ComponentManagerAPI.Services.DataTransformation
{

    public class ComponentJsonConverter : JsonConverter
    {
        public ComponentJsonConverter(
            ICodeContractService codeContractDependency,
            IComponentConstructionData componentConstructionDependency,
            IComponentDataModel<IComponentAdapter, IComponentConstructionData> componentDataModelDependency,
            IGenericInjectionFactory componentFactoryDependency,
            IServiceProvider serviceProvDep)
        {
            _servProvDep = serviceProvDep;
            _codeContractDependency = codeContractDependency;
            _componentConstructionDependency = componentConstructionDependency;
            _componentDataModelDependency = componentDataModelDependency;
            _componentFactoryDependency = componentFactoryDependency;
        }

        private readonly IServiceProvider _servProvDep;
        private readonly ICodeContractService _codeContractDependency;
        private readonly IComponentConstructionData _componentConstructionDependency;
        private readonly IComponentDataModel<IComponentAdapter, IComponentConstructionData> _componentDataModelDependency;
        private readonly IGenericInjectionFactory _componentFactoryDependency;

        public override bool CanConvert(Type objectType)
        {
            //ensure non null type provided
            _codeContractDependency.Requires<ArgumentNullException>(objectType != null, "");
            //Can convert if supplied object is or derives from expected type
            bool canConvert = typeof(IComponentDataModel<IComponentAdapter, IComponentConstructionData>).IsAssignableFrom(objectType);
            return canConvert;

        }

        //dont allow this converter to write json
        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {

            //TODO: HOOK ERRORS UP TO EMAIL SERVER
            serializer.Error += (sender, args) =>
            {
                var c = args.ErrorContext.Error;
                args.ErrorContext.Handled = true;
            };

            //null checking
            _codeContractDependency.Requires<ArgumentNullException>(reader != null, "");
            _codeContractDependency.Requires<ArgumentNullException>(objectType != null, "");
            _codeContractDependency.Requires<ArgumentNullException>(serializer != null, "");


            // Load JObject from reader
            JObject jObject = JObject.Load(reader);

            //init expected type
            Type expectedType = typeof(IComponentDataModel<IComponentAdapter, IComponentConstructionData>);

            //ensure correct type
            _codeContractDependency.Requires<InvalidCastException>(expectedType.IsAssignableFrom(objectType), "");

            //init ref to jobject values for ctor obj and comp
            var constructionObj = jObject[nameof(_componentDataModelDependency.ConstructionData)];
            var componentObj = jObject[nameof(_componentDataModelDependency.ComponentAdapterProfile)];

            //ensure both values found
            _codeContractDependency.Requires<ArgumentNullException>(constructionObj != null && componentObj != null, "");

            //see if they have values
            bool hasComponent = componentObj.HasValues;
            bool hasCtor = constructionObj.HasValues;

            //ensure they have values
            _codeContractDependency.Requires<ArgumentNullException>(hasComponent && hasCtor, "");

            //load inner Jobject for ctor obj
            JObject constructionjObj = JObject.Load(constructionObj.CreateReader());

            ////init ref to jobject values for assembly and class name
            var assemblyPathObj = constructionjObj[nameof(_componentConstructionDependency.AssemblyPath)];
            var classNameObj = constructionjObj[nameof(_componentConstructionDependency.ClassName)];

            // Populate the construction data object
            serializer.Populate(constructionjObj.CreateReader(), _componentConstructionDependency);
            //ensure ctor data not null
            _codeContractDependency.Requires<ArgumentNullException>(_componentConstructionDependency != null, "");
            // try get assembly path and class name 
            string assemblyPath = _componentConstructionDependency.AssemblyPath;
            string className = _componentConstructionDependency.ClassName;


            //ensure class and assembly non null
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(assemblyPath), "");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(className), "");


            //create correct component type
            IComponentAdapter componentObject =
                (IComponentAdapter)_componentFactoryDependency
                .Construct(assemblyPath: assemblyPath, className: className, serviceProviderDep: _servProvDep, targetType: typeof(IComponentAdapter));
            //ensure valid type provided
            _codeContractDependency.Requires<TypeInitializationException>(typeof(IComponentAdapter).IsAssignableFrom(componentObject.GetType()), "");

            //load inner Jobject for ctor obj
            JObject compjObj = JObject.Load(componentObj.CreateReader());


            //populate component object
            serializer.Populate(compjObj.CreateReader(), componentObject);

            //set component profile and construction data profile to populated comp profile
            _componentDataModelDependency.ComponentAdapterProfile = componentObject;
            _componentDataModelDependency.ConstructionData = _componentConstructionDependency;



            //return populated construction object
            return _componentDataModelDependency;

        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            //should never be called
            throw new NotImplementedException();
        }
    }
}
