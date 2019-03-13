using CommonServiceInterfaces;
using ComponentInterfaces;
using ComponentManagerAPI.Middleware;
using ComponentManagerAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;

namespace ComponentManagerAPI.Controllers
{

    /// <summary>
    /// Open generic type definition controller for general use type administration. 
    /// Can not be used by the asp.net runtime since abstract and open generic type definition.
    /// </summary>
    [ServiceFilter(typeof(IExceptionFilter))]//factory attr used to resolve and instantiate the required mvc filter from IoC container(registered in startup)
    [Route("API/Components/")]
    public class ComponentsController : Controller
    {

        public ComponentsController(
            ICodeContractService codeContractDependency,
            IActionTransformService actionTransformDependency,
            IComponentManager<IComponentAdapter, IComponentConstructionData> componentManagerDependency,
            IActionResultWrapperService actionResultWrapperDependency,
            IAssemblyProbing assemblyProbingDependency)
        {
            _codeContractDependency = codeContractDependency;
            _actionTransformDependency = actionTransformDependency;
            _componentManagerDependency = componentManagerDependency;
            _actionResultWrapperDependency = actionResultWrapperDependency;
            _assemblyProbingDependency = assemblyProbingDependency;
        }

        #region IOC_INJECTED_MEMBERS

        protected readonly ICodeContractService _codeContractDependency;
        protected readonly IActionTransformService _actionTransformDependency;
        protected readonly IComponentManager<IComponentAdapter, IComponentConstructionData> _componentManagerDependency;
        protected readonly IActionResultWrapperService _actionResultWrapperDependency;
        protected readonly IAssemblyProbing _assemblyProbingDependency;

        #endregion


        #region HELPER_SERVICES


        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}/Actions/{ActionID}/Uri", Name = "GetComponentActionUri")]
        [HttpGet("/Profiles/Actions/Uri", Name = "GetComponentActionUriFromQueryStr")]
        public IActionResult GetComponentActionUrl(string ComponentID, string ProfileName, string ActionID)
        {

            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ComponentID), "Failed to get component action URI. Component ID cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ProfileName), "Failed to get component action URI. Profile name cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ActionID), "Failed to get component action URI. Action id cannot be empty");

            string actionURI = Url.RouteUrl(
                "InitializeComponentAndInvokeAction",
                new { ComponentID = ComponentID, ProfileName = ProfileName, ActionID = ActionID },
                "http",
                HttpContext.Request.Host.Host);


            //get empty construction profile
            return _actionResultWrapperDependency.GenerateOkActionResult(actionURI, this);
        }

        #endregion

        #region COMPONENT_OBJECTS

        [HttpPost("{" + nameof(IComponentConstructionData.ComponentID) + "}/", Name = "CreateComponentObject")]
        public IActionResult CreateComponentObject(string ComponentID, string CreatedBy = "Unknown")
        {
            _componentManagerDependency.CreateComponent(ComponentID, CreatedBy);

            //get empty construction profile
            return _actionResultWrapperDependency.GenerateOkActionResult();
        }

        [HttpDelete("{" + nameof(IComponentConstructionData.ComponentID) + "}/", Name = "DeleteComponentObject")]
        public IActionResult DeleteComponentObject(string ComponentID)
        {

            _componentManagerDependency.DeleteComponent(ComponentID);
            return _actionResultWrapperDependency.GenerateOkActionResult();
        }

        [HttpPut("{" + nameof(IComponentConstructionData.ComponentID) + "}/Name/{NewComponentID}", Name = "RenameComponentObject")]
        public IActionResult RenameComponentObject(string ComponentID, string NewComponentID, string UpdatedBy = "Unknown")
        {

            _componentManagerDependency.RenameComponent(ComponentID, NewComponentID, UpdatedBy);

            return _actionResultWrapperDependency.GenerateOkActionResult();
        }

        [HttpGet("Identifiers/", Name = "GetComponentObjectIdentifiers")]
        public IActionResult GetComponentIDs()
        {

            var componentIDs = _componentManagerDependency
                .GetConstructionDataProfiles()?
                .Select(p => p.ComponentID)?
                .Distinct()?
                .ToArray();

            return _actionResultWrapperDependency.GenerateOkActionResult(componentIDs, this);
        }

        [HttpGet("", Name = "GetComponentObjects")]
        public IActionResult GetComponentObjects()
        {


            var components = _componentManagerDependency
                .GetConstructionDataProfiles()?
                .GroupBy(p => p.ComponentID)?
                .Select(p => p.FirstOrDefault())?
                .ToArray();

            return _actionResultWrapperDependency.GenerateOkActionResult(components, this, "~/Views/ComponentObjects/_ComponentCollection.cshtml");
        }

        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/", Name = "GetComponentObjectProfiles")]
        public IActionResult GetComponentObjectProfiles(string ComponentID)
        {

            //get particular or all component objects
            var components = _componentManagerDependency.GetConstructionDataProfiles()?.Where(cp => cp.ComponentID.Equals(ComponentID, StringComparison.Ordinal)).ToArray();

            return _actionResultWrapperDependency.GenerateOkActionResult(components, this, "~/Views/ComponentObjects/_ComponentProfileCollection.cshtml");
        }

        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Adapters/Profiles/Identifiers", Name = "GetComponentObjectAdapterProfileIdentifiers")]
        [HttpGet("/Profiles/Identifiers", Name = "GetComponentObjectAdapterProfileIdentifiersFromQueryStr")]
        public IActionResult GetComponentProfileNames(string ComponentID)
        {

            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ComponentID), "Failed to get component profile names. Component ID cannot be empty");

            var adapterProfileNames = _componentManagerDependency
                .GetConstructionDataProfiles()?
                .Where(c => c.ComponentID.Equals(ComponentID, StringComparison.Ordinal))?
                .Select(c => c.AdapterProfileName)?
                .Distinct()?
                .ToArray();

            return _actionResultWrapperDependency.GenerateOkActionResult(adapterProfileNames, this);
        }

        #endregion

        #region COMPONENT_OBJECT_PROFILES


        [HttpGet("Adapters/{" + nameof(IComponentConstructionData.ClassName) + "}", Name = "GetComponentObjectAdapter")]
        [HttpGet("Adapters", Name = "GetComponentObjectAdapterFromQueryStr")]
        public IActionResult GetComponentObjectAdapter(string AssemblyPath, string ClassName)
        {

            var component = _componentManagerDependency.GetAdapter(AssemblyPath, ClassName);

            return _actionResultWrapperDependency.GenerateOkActionResult(component, this, "~/Views/ComponentObjects/_Adapter.cshtml");
        }

        [HttpPost("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}", Name = "CreateComponentObjectProfile")]
        public IActionResult CreateComponentObjectProfile(string ComponentID, string ProfileName, string CreatedBy = "Unknown")
        {

            _componentManagerDependency.CreateComponentProfile(ComponentID, ProfileName, CreatedBy);

            return _actionResultWrapperDependency.GenerateOkActionResult();
        }

        [HttpPut("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}/Name", Name = "RenameComponentObjectProfile")]
        public IActionResult RenameComponentObjectProfile(string ComponentID, string ProfileName, string NewName)
        {

            _componentManagerDependency.RenameComponentProfile(componentID: ComponentID, oldProfileName: ProfileName, newProfileName: NewName);

            return _actionResultWrapperDependency.GenerateOkActionResult();
        }


        [HttpPut("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}", Name = "UpdateComponentObjectProfile")]
        public IActionResult UpdateComponentObjectProfile(string ComponentID, string ProfileName, IComponentAdapter Adapter, IComponentConstructionData ConstructionData)
        {
            string failurePrefix = "Failed to update component profile. ";
            _codeContractDependency.Requires<ArgumentNullException>(ConstructionData != null, failurePrefix + "Construction data cannot be null");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ComponentID), failurePrefix + "Component ID cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ConstructionData.ComponentID), failurePrefix + "Component ID cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ProfileName), failurePrefix + "Profile name cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ConstructionData.AdapterProfileName), failurePrefix + "Profile name cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(
                ProfileName.Equals(ConstructionData.AdapterProfileName, StringComparison.InvariantCulture),
                failurePrefix + "Route supplied profile name: " + ProfileName + " did not match the body supplied profile name: " + ConstructionData.AdapterProfileName);

            _codeContractDependency.Requires<ArgumentNullException>(
                ComponentID.Equals(ConstructionData.ComponentID, StringComparison.InvariantCulture),
                failurePrefix + "Route supplied component Id: " + ComponentID + " did not match the body supplied component Id: " + ConstructionData.ComponentID);

            _componentManagerDependency.UpdateComponentProfile(Adapter, ConstructionData);

            return _actionResultWrapperDependency.GenerateOkActionResult();
        }

        [HttpDelete("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}", Name = "DeleteComponentObjectProfile")]
        public IActionResult DeleteComponentObjectProfile(string ComponentID, string ProfileName)
        {

            _componentManagerDependency.DeleteComponentProfile(ComponentID, ProfileName);

            return _actionResultWrapperDependency.GenerateOkActionResult();
        }

        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}", Name = "GetComponentObjectProfile")]
        public IActionResult GetComponentObjectProfile(string ComponentID, string ProfileName)
        {

            //get comp construction profile
            IComponentDataModel<IComponentAdapter, IComponentConstructionData> component = _componentManagerDependency.GetComponentProfile(ComponentID, ProfileName);

            return _actionResultWrapperDependency.GenerateOkActionResult(component, this, "~/Views/ComponentObjects/_Component.cshtml");

        }

        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/Active", Name = "GetActiveComponentObjectProfile")]
        public IActionResult GetActiveComponentObjectProfile(string ComponentID)
        {

            var component = _componentManagerDependency.GetActiveComponentProfile(ComponentID);

            return _actionResultWrapperDependency.GenerateOkActionResult(component, this);

        }

        [HttpPut("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/Active/{ProfileName}", Name = "SetActiveComponentObjectProfile")]
        public IActionResult SetActiveComponentObjectProfile(string ComponentID, string ProfileName)
        {
            //set comp construction profile
            _componentManagerDependency.SetActiveComponentProfile(ComponentID, ProfileName);
            //return new active comp construction profile
            return _actionResultWrapperDependency.GenerateOkActionResult();
        }


        #endregion

        #region COMPONENT_OBJECT_ACTIONS

        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}/Actions/Identifiers", Name = "GetActionIdentifiersOnComponentObjectProfile")]
        [HttpGet("Profiles/Actions/Identifiers", Name = "GetActionIdentifiersOnComponentObjectProfileFromQueryStr")]
        public IActionResult GetActionIdentifiersOnComponentObjectProfile(string ComponentID, string ProfileName)
        {
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ComponentID), "Failed to get component action IDs. Component ID cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ProfileName), "Failed to get component action IDs. Profile name cannot be empty");

            //get actions
            var actions = _componentManagerDependency.GetComponentProfileActions(ComponentID, "", ProfileName);
            //transform actions to actionstructure
            var actionsStructures = _actionTransformDependency.ConvertToActionStructureCollection(actions);
            //select action IDs
            var actionIds = actionsStructures?
                .Select(a => a.MemberID)
                .ToArray();

            return _actionResultWrapperDependency.GenerateOkActionResult(actionIds, this);

        }

        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}/Actions/{ActionID}", Name = "GetActionsOnComponentObjectProfile")]
        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}/Actions", Name = "GetActionsOnComponentObjectProfile_All")]
        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/Active/Actions/{ActionID}", Name = "GetActionsOnComponentObjectProfile_Active")]
        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/Active/Actions", Name = "GetActionsOnComponentObjectProfile_Active_All")]
        public IActionResult GetActionsOnComponentObjectProfile(string ComponentID, string ActionID = "", string ProfileName = "")
        {

            //get all actions
            IEnumerable<MethodInfo> actions = _componentManagerDependency.GetComponentProfileActions(ComponentID, ActionID, ProfileName);
            //transform actions to actionstructure
            var actionsStructures = _actionTransformDependency.ConvertToActionStructureCollection(actions);

            //update with valid action urls
            IList<IComponentActionStructure> _actionsStructures =
                actionsStructures
                .Select(a =>
                {

                    //set uri
                    a.ActionURL = string.IsNullOrEmpty(ProfileName) ?
                    Url.RouteUrl("InitializeComponentAndInvokeAction_Active", new { ComponentID = ComponentID, ActionID = a.MemberID })
                    :
                    Url.RouteUrl("InitializeComponentAndInvokeAction", new { ComponentID = ComponentID, ProfileName = ProfileName, ActionID = a.MemberID });
                    return a;
                }).ToList();

            ViewBag.ComponentID = ComponentID;
            ViewBag.ProfileName = ProfileName;

            return _actionResultWrapperDependency.GenerateOkActionResult(_actionsStructures, this, "~/Views/ComponentObjects/_ComponentActions.cshtml");

        }

        [HttpPut("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}/Actions/{ActionID}", Name = "InvokeActionOnComponentObjectProfile")]//invoke on Specified profile
        [HttpPut("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/Active/Actions/{ActionID}", Name = "InvokeActionOnComponentObjectProfile_Active")]//invoke on active profile
        [HttpPut("Profiles/Actions", Name = "InvokeActionOnComponentObjectProfileFromQueryStr")]//invoke on Specified profile, get args from places in request other than route params
        public IActionResult InvokeActionOnComponentObjectProfile(string ComponentID, string ActionID, [FromBody]Dictionary<string, string> ActionArguments, string ProfileName = "")
        {

            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ActionID), "Failed to Invoke action on component. Action ID cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ComponentID), "Failed to Invoke action on component. Component ID cannot be empty");

            //invoke method on component
            object retVal = _componentManagerDependency.ConnectAndInvokeActionOnComponentProfile(ComponentID, ActionID, ActionArguments, ProfileName);

            return _actionResultWrapperDependency.GenerateOkActionResult(retVal ?? "OK", this);
        }



        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}/State", Name = "ConnectComponentAndReadState")]//invoke on Specified profile
        [HttpGet("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/Active/State", Name = "ConnectComponentAndReadState_Active")]//invoke on active profile
        public IActionResult ConnectAndReadStateComponentObjectProfile(string ComponentID, string ProfileName = "")
        {
            //ensure comp id supplied
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ComponentID), "Failed to read component state. Component ID cannot be empty");

            var component = _componentManagerDependency.GetComponentProfile(ComponentID, ProfileName);
            //connect, read comp state, then return component
            component.ComponentAdapterProfile = _componentManagerDependency.GetComponentProfileStates(ComponentID, ProfileName);

            return _actionResultWrapperDependency.GenerateOkActionResult(component, this, "~/Views/ComponentObjects/_ComponentState.cshtml");
        }


        [HttpPut("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/{ProfileName}/Actions/{ActionID}/Dependent", Name = "InitializeComponentAndInvokeAction")]//invoke on Specified profile
        [HttpPut("{" + nameof(IComponentConstructionData.ComponentID) + "}/Profiles/Active/Actions/{ActionID}/Dependent", Name = "InitializeComponentAndInvokeAction_Active")]//invoke on active profile
        public IActionResult InitializeAndInvokeMethodOnComponentObjectProfile(string ComponentID, string ActionID, ICollection<IComponentActionStructure> Actions, string ProfileName = "")
        {
            _codeContractDependency.Requires<ArgumentNullException>(Actions != null && Actions.Count > 0, "Failed to initialize and invoke action on component. Actions cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ActionID), "Failed to initialize and invoke action on component. Action ID cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ComponentID), "Failed to initialize and invoke action on component. Component Id cannot be empty");

            //get correct action
            var action = Actions.SingleOrDefault(a => a.MemberID.Equals(ActionID, StringComparison.Ordinal));
            //get independent flag
            bool isIndependent = action.IsIndependent;
            //get init flag
            bool RequiresInitialize = action.RequiresInitialize;

            //attempt to build dictionary of args
            Dictionary<string, string> ActionArguments =
                action
                .Parameters?
                .Select(p => p)?
                .ToDictionary(
                    p => p.MemberName,
                    p =>
                    {
                        return p.ParameterValue?.ToString() ?? string.Empty;
                    });

            object retVal;

            //if is independent
            if (isIndependent)
            {
                //invoke without connection or init
                retVal = _componentManagerDependency.InvokeActionOnComponentProfile(ComponentID, ActionID, ActionArguments, ProfileName);
            }
            else
            {
                //action requires init
                if (RequiresInitialize)
                {
                    retVal = _componentManagerDependency.InitializeAndInvokeActionOnComponentProfile(ComponentID, ActionID, ActionArguments, ProfileName);
                }
                //if stand alone action
                else
                {
                    //invoke method on component
                    retVal = _componentManagerDependency.ConnectAndInvokeActionOnComponentProfile(ComponentID, ActionID, ActionArguments, ProfileName);
                }
            }


            //return _actionResultWrapperDependency.GenerateOkActionResult(retVal ?? "OK", this);
            return _actionResultWrapperDependency.GenerateOkActionResult(retVal ?? "OK", this, "~/Views/ComponentObjects/_ComponentActionResult.cshtml");
        }


        #endregion

    }

}
