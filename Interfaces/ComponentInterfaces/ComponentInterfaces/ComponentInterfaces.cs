using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ComponentInterfaces
{
    /// <summary>
    /// Defines a contract for any actuating device such as a motor. Superset of IComponentAdapter.
    /// </summary>
    public interface IActuator : IComponentAdapter
    {
        /// <summary>
        /// Perform action required to establish frame of reference
        /// </summary>
        void Home();
    }

    /// <summary>
    /// Defines a contract for any device that can move to a point within a three dimensional cartesian space
    /// </summary>
    public interface ICartesianRobotAdapter : IActuator
    {
        /// <summary>
        /// Move to point in space relative to devices frame of reference
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        void MoveToCoordinates(int x, int y, int z);
        /// <summary>
        /// Move to a teachpoint. A teachpoint is an alias/surrogate for coordinates.
        /// </summary>
        /// <param name="teachPoint"></param>
        void MoveToTeachpoint(string teachPoint);
        /// <summary>
        /// Return current coordinates of the device relative to the current frame of reference.
        /// </summary>
        /// <returns>Current coordinates</returns>
        ICartesianCoordinates GetCurrentCoordinates();
        /// <summary>
        /// Attempt to map the current coordinates to a teachpoint
        /// </summary>
        /// <returns>teachpoint identifier</returns>
        string GetCurrentTeachPoint();

        /// <summary>
        /// Move o the devices configured docking position. This should be a neutral position used for storing the device.
        /// </summary>
        void Dock();
    }


    /// <summary>
    /// Defines a contract for device metadata required to drive a device as an end effector. 
    /// </summary>
    public interface ICartesianEndEffectorMetadata
    {
        /// <summary>
        /// Human readable name of end effector
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// The component ID. Should match a component registered with the component manager
        /// </summary>
        string ComponentID { get; set; }
        /// <summary>
        /// The component profile name. Should match a component registered with the component manager
        /// </summary>
        string AdapterProfile { get; set; }
        /// <summary>
        /// Human readable description of end effector
        /// </summary>
        string Description { get; set; }
        /// <summary>
        /// X axis offset relative to teachpoint for end effector to operate correctly on target. 
        /// </summary>
        int XOffset { get; set; }
        /// <summary>
        /// Y axis offset relative to teachpoint for end effector to operate correctly on target. 
        /// </summary>
        int YOffset { get; set; }
        /// <summary>
        /// Z axis offset relative to teachpoint for end effector to operate correctly on target. 
        /// </summary>
        int ZOffset { get; set; }
    }

    /// <summary>
    /// Defines a contract for specifying coordinates of a point in cartesian space
    /// </summary>
    public interface ICartesianCoordinates
    {
        /// <summary>
        /// 
        /// </summary>
        int X_Axis { get; set; }

        /// <summary>
        /// 
        /// </summary>
        int Y_Axis { get; set; }

        /// <summary>
        /// 
        /// </summary>
        int Z_Axis { get; set; }
    }

    /// <summary>
    /// Defines a contract for a component capable of actuating to a specified position i.e. a servo actuator. 
    /// Component should actuate through discrete positions rather than continuum. Superset of IActuator.
    /// </summary>
    public interface IBidirectionalServoActuatorAdapter : IActuator
    {
        /// <summary>
        /// Actuate continuously at specified speed
        /// </summary>
        /// <param name="speed"></param>
        void Actuate(int speed);
        /// <summary>
        /// Actuate to specified position relative to current frame of reference
        /// </summary>
        /// <param name="position"></param>
        /// <returns>Unbiased measurement of position after move</returns>
        int ServoActuate(int position);
        /// <summary>
        /// Get current position relative to current frame of reference.
        /// </summary>
        /// <returns>Current position</returns>
        int GetPosition();
    }

    /// <summary>
    /// Defines a contract for a temperature dependent pH sensor. Superset of ISensorAdapter
    /// </summary>
    public interface IpHSensorAdapter : ISensorAdapter
    {
        /// <summary>
        /// Compensation temperature to adjust measured pH
        /// </summary>
        double CompensationTemperature { get; set; }

        /// <summary>
        /// Most recent standard deviation for an averaged pH reading
        /// </summary>
        double StandardDeviation { get; set; }
    }

    /// <summary>
    /// Defines contract for a generic sensor e.g. temperature, humidity, strain, barcode, etc. Superset of IComponentAdapter
    /// </summary>
    public interface ISensorAdapter : IComponentAdapter
    {
        /// <summary>
        /// Measure parameter of interest
        /// </summary>
        /// <returns>value of measurement</returns>
        string Scan();
    }


    /// <summary>
    /// Defines a contract for a generic device. Superset of IDisposable and IServiceProviderInjected.
    /// </summary>
    public interface IComponentAdapter : IDisposable
    {
        /// <summary>
        /// Setup routine required to get component in valid initial state
        /// </summary>
        void Initialize();
        /// <summary>
        /// Error state from component
        /// </summary>
        /// <returns>error state</returns>
        string GetError();
        /// <summary>
        /// interrupt component and cancel current operation
        /// </summary>
        void Stop();
        /// <summary>
        /// interrupt component and pause current operation
        /// </summary>
        void Pause();
        /// <summary>
        /// Resume previously pause operation
        /// </summary>
        void Resume();
        /// <summary>
        /// Interrupt component and reset
        /// </summary>
        void Reset();
        /// <summary>
        /// Shut down routine required to get component in valid dormant state
        /// </summary>
        void ShutDown();
        /// <summary>
        /// Connect to component
        /// </summary>
        void Connect();
        /// <summary>
        /// Disconnect from component
        /// </summary>
        void Disconnect();
        /// <summary>
        /// Get connection state
        /// </summary>
        bool IsConnected();
        /// <summary>
        /// Commit any volatile configuration data
        /// </summary>
        void CommitConfiguredState();
        /// <summary>
        /// Read component state
        /// </summary>
        void ReadState();

        /// <summary>
        /// Inject a service provider to extend the reach of dependency injection
        /// </summary>
        void InjectServiceProvider(IServiceProvider servProv);

        /// <summary>
        /// Human readable component identifier
        /// </summary>
        string ComponentName { get; set; }


    }


    /// <summary>
    /// Defines contract for type-safe, covariant wrapper, consolidating two objects as properties. 
    /// These props are enforced by runtime type-checking against specified generic type args. 
    /// </summary>
    /// <typeparam name="ComponentAdapterType">Type requirement for ComponentAdapterProfile property. If dont care, specify typeof(object)</typeparam>
    /// <typeparam name="ConstructionDataType">Type requirement for ConstructionData property. If dont care, specify typeof(object)</typeparam>
    public interface IComponentDataModel<out ComponentAdapterType, out ConstructionDataType>
    {
        /// <summary>
        /// Metadata required to dynamically load assembly and instantiate a type
        /// </summary>
        object ConstructionData { get; set; }
        /// <summary>
        /// The dynamically loaded type object
        /// </summary>
        object ComponentAdapterProfile { get; set; }
    }

    /// <summary>
    /// Defines contract of metadata required to dynamically load assembly and instantiate component type
    /// </summary>
    public interface IComponentConstructionData
    {
        /// <summary>
        /// Human readable component identifier
        /// </summary>
        string ComponentID { get; set; }
        /// <summary>
        /// Human readable component profile identifier
        /// </summary>
        string AdapterProfileName { get; set; }
        /// <summary>
        /// Directory where component profile is stored
        /// </summary>
        string AdapterProfileDirectory { get; set; }
        /// <summary>
        /// Flag specifying whether profile is the active one
        /// </summary>
        bool IsActive { get; set; }
        /// <summary>
        /// Date profile was created
        /// </summary>
        DateTime DateCreated { get; set; }
        /// <summary>
        /// Identity of creator
        /// </summary>
        string CreatedBy { get; set; }
        /// <summary>
        /// Date of profile modification
        /// </summary>
        DateTime DateModified { get; set; }
        /// <summary>
        /// Identity of modifier
        /// </summary>
        string ModifiedBy { get; set; }
        /// <summary>
        /// Path to assembly where component is located
        /// </summary>
        string AssemblyPath { get; set; }
        /// <summary>
        /// Class name of the component
        /// </summary>
        string ClassName { get; set; }

    }

    /// <summary>
    /// Defines contract of closed generic type capable of resolving the equality of two components
    /// </summary>
    public interface IComponentEqualityComparer : IEqualityComparer<IComponentConstructionData>
    {

    }

    /// <summary>
    /// Defines contract for a open generic component manager, providing all tooling for 
    /// performing CRUD operations on components and associated profiles. 
    /// </summary>
    /// <typeparam name="ComponentAdapterType">Type of the component to manage</typeparam>
    /// <typeparam name="ConstructionDataType">Type of metadata required to manage component</typeparam>
    public interface IComponentManagerBase<ComponentAdapterType, ConstructionDataType>
    {
        #region CONSTRUCTION
        /// <summary>
        /// Commit current state of component collection
        /// </summary>
        void PersistConstructionDataCollection();
        /// <summary>
        /// Get collection of component metadata
        /// </summary>
        /// <returns></returns>
        IComponentCollection<ConstructionDataType> GetConstructionDataProfiles();
        #endregion


        #region COMPONENT
        /// <summary>
        /// Create new component
        /// </summary>
        /// <param name="componentID">Human readable component identifier</param>
        /// <param name="createdBy">Identity of creator</param>
        void CreateComponent(string componentID, string createdBy);
        /// <summary>
        /// Delete component
        /// </summary>
        /// <param name="componentID">component identifier</param>
        void DeleteComponent(string componentID);
        /// <summary>
        /// Change a component identifier
        /// </summary>
        /// <param name="oldComponentID">Previous identifier</param>
        /// <param name="newComponentID">New identifier</param>
        /// <param name="updatedBy">Identity of updater</param>
        void RenameComponent(string oldComponentID, string newComponentID, string updatedBy = "Unknown");
        #endregion

        #region PROFILE
        /// <summary>
        /// Delete a component profile
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <param name="adapterProfileName">Profile identifier</param>
        void DeleteComponentProfile(string componentID, string adapterProfileName);

        /// <summary>
        /// Create component profile
        /// </summary>
        /// <param name="componentID">Component identifier</param>
        /// <param name="adapterProfileName">Profile identifier</param>
        /// <param name="createdBy">Identity of creator</param>
        void CreateComponentProfile(string componentID, string adapterProfileName, string createdBy);

        /// <summary>
        /// Rename a component profile
        /// </summary>
        /// <param name="componentID">Component identifier</param>
        /// <param name="oldProfileName">Old profile name</param>
        /// <param name="newProfileName">New profile name</param>
        /// <param name="updatedBy">Who is carrying out the update</param>
        void RenameComponentProfile(string componentID, string oldProfileName, string newProfileName, string updatedBy = "Unknown");

        /// <summary>
        /// Update component profile
        /// </summary>
        /// <param name="adapterProfile">Profile object</param>
        /// <param name="constructionProfile">Profile metadata object</param>
        void UpdateComponentProfile(ComponentAdapterType adapterProfile, ConstructionDataType constructionProfile);

        /// <summary>
        /// Get component profile
        /// </summary>
        /// <param name="componentID">Component identifier</param>
        /// <param name="profileName">Profile identifier</param>
        /// <returns></returns>
        IComponentDataModel<ComponentAdapterType, ConstructionDataType> GetComponentProfile(string componentID, string profileName);

        /// <summary>
        /// Get active component profile
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <returns></returns>
        IComponentDataModel<ComponentAdapterType, ConstructionDataType> GetActiveComponentProfile(string componentID);

        /// <summary>
        /// Set the active component profile
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <param name="profileName">profile identifier</param>
        void SetActiveComponentProfile(string componentID, string profileName);

        /// <summary>
        /// Get collection of component profiles
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <returns></returns>
        IComponentCollection<ConstructionDataType> GetComponentProfiles(string componentID);

        /// <summary>
        /// Get collection of component profile names
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <returns></returns>
        IEnumerable<string> GetComponentProfileNames(string componentID);

        /// <summary>
        /// Get a new compoent profile object
        /// </summary>
        /// <param name="assemblyName">Path to assembly where component type resides</param>
        /// <param name="className">Component class name</param>
        /// <returns></returns>
        ComponentAdapterType GetAdapter(string assemblyName, string className);
        #endregion

        #region METHODS

        /// <summary>
        /// Get collection of component profile methods
        /// </summary>
        /// <param name="componentAdapterProfile">component profile object</param>
        /// <returns></returns>
        IEnumerable<MethodInfo> GetComponentProfileMethods(object componentAdapterProfile);

        /// <summary>
        /// /// Get collection of component profile methods
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <param name="methodName">method name</param>
        /// <param name="profileName">profile identifier</param>
        /// <returns></returns>
        IEnumerable<MethodInfo> GetComponentProfileMethods(string componentID, string methodName = "", string profileName = "");

        /// <summary>
        /// Get collection of component profile actions. 
        /// It is up to the implementor to make the distinction criteria between methods and actions.
        /// Generally, should be a more curated collection
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <param name="actionID">action identifier</param>
        /// <param name="profileName">profile identifier</param>
        /// <returns></returns>
        IEnumerable<MethodInfo> GetComponentProfileActions(string componentID, string actionID = "", string profileName = "");


        #endregion
    }

    /// <summary>
    /// Defines contract for a open, constrained, generic component manager, providing all tooling for 
    /// performing CRUD operations on components and associated profiles. Superset of IComponentManagerBase.
    /// </summary>
    /// <typeparam name="ComponentAdapterType">Type of the component to manage. Must be superset of or implement IComponentAdapter</typeparam>
    /// <typeparam name="ConstructionDataType">Type of metadata required to manage component</typeparam>
    public interface IComponentManager<ComponentAdapterType, ConstructionDataType> : IComponentManagerBase<ComponentAdapterType, ConstructionDataType>
        where ComponentAdapterType : IComponentAdapter
    {

        #region ACTIONS
        /// <summary>
        /// Initialize component
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <param name="profile">profile identifier</param>
        void InitializeComponentProfile(string componentID, string profile = "");

        /// <summary>
        /// Invoke specified action on component profile
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <param name="actionID">action identifier</param>
        /// <param name="actionArgs">action arguments. Should be correct order and number expected by the action</param>
        /// <param name="profileName">component profile identifier</param>
        /// <returns>The return object of the invoked method</returns>
        object InvokeActionOnComponentProfile(string componentID, string actionID, Dictionary<string, string> actionArgs = null, string profileName = "");

        /// <summary>
        /// initialize and invoke specified action on component profile
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <param name="actionID">action identifier</param>
        /// <param name="actionArgs">action arguments. Should be correct order and number expected by the action</param>
        /// <param name="profileName">Component profile identifier</param>
        /// <returns>The return object of the invoked method</returns>
        object InitializeAndInvokeActionOnComponentProfile(string componentID, string actionID, Dictionary<string, string> actionArgs, string profileName = "");

        /// <summary>
        /// Connect and invoke specified action on component profile
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <param name="actionID">action identifier</param>
        /// <param name="actionArgs">action arguments. Should be correct order and number expected by the action</param>
        /// <param name="profileName">Component profile identifier</param>
        /// <returns>The return object of the invoked method</returns>
        object ConnectAndInvokeActionOnComponentProfile(string componentID, string actionID, Dictionary<string, string> actionArgs, string profileName = "");
        #endregion

        #region STATE

        /// <summary>
        /// Read the components state
        /// </summary>
        /// <param name="componentID">component identifier</param>
        /// <param name="profileName">profile identifier</param>
        /// <returns></returns>
        ComponentAdapterType GetComponentProfileStates(string componentID, string profileName = "");

        #endregion

    }


    /// <summary>
    /// Defines contract for covariant, type-safe collection of component metadata.
    /// Superset of IEnumerable[ConstructionDataType], IEnumerable.
    /// </summary>
    /// <typeparam name="ConstructionDataType"></typeparam>
    public interface IComponentCollection<out ConstructionDataType> : IEnumerable<ConstructionDataType>, IEnumerable
    {
        /// <summary>
        /// Add new component metdata to collection
        /// </summary>
        /// <param name="item"></param>
        /// <returns>Successful add flag</returns>
        bool AddComponent(object item);

        /// <summary>
        /// Remove existing component metdata from collection
        /// </summary>
        /// <param name="item"></param>
        /// <returns>Successful remove flag</returns>
        bool RemoveComponent(object item);
    }

    /// <summary>
    /// Defines contract for an attribute designating a property as component state.
    /// Superset of IHumanReadableMember.
    /// </summary>
    public interface IComponentStateAttribute : _Attribute, IHumanReadableMember
    {

    }

    /// <summary>
    /// Defines contract for an attribute designating a method as a component action.
    /// Superset of IHumanReadableMember.
    /// </summary>
    public interface IComponentActionAttribute : _Attribute, IHumanReadableMember
    {
        /// <summary>
        /// whether an aciton can be run independently
        /// </summary>
        bool IsIndependent { get; set; }
    }

    /// <summary>
    ///  Defines contract for an attribute designating a method parameter as an action parameter
    /// </summary>
    public interface IComponentActionParameterAttribute : _Attribute, IHumanReadableMember
    {

    }

    /// <summary>
    /// Defines contract for metadata required to invoke web-hosted component action remotely.
    /// Superset of IHumanReadableMember.
    /// </summary>
    public interface IComponentActionStructure : IHumanReadableMember
    {
        /// <summary>
        /// url endpoint of component action
        /// </summary>
        string ActionURL { get; set; }
        /// <summary>
        /// Flag specifying whether component needs to be initialized before invoking action
        /// </summary>
        bool RequiresInitialize { get; set; }
        /// <summary>
        /// Flag specifying whether an action can be invoked independently
        /// </summary>
        bool IsIndependent { get; set; }
        /// <summary>
        /// Collection of parameter objects
        /// </summary>
        IList<IComponentActionParameterStructure> Parameters { get; set; }
    }


    /// <summary>
    /// Defines a contract for component action parameter. Superset of IHumanReadableMember.
    /// </summary>
    public interface IComponentActionParameterStructure : IHumanReadableMember
    {
        /// <summary>
        /// The type of the parameter.
        /// </summary>
        Type ParameterType { get; set; }
        /// <summary>
        /// The string representation of the param value
        /// </summary>
        string ParameterValue { get; set; }
    }

    /// <summary>
    /// Defines a contract for metadata that describes where an assembly probe should look when attempting to resolve an assembly.
    /// </summary>
    public interface IAssemblyProbing
    {
        /// <summary>
        /// stores the most recent probing location
        /// </summary>
        string CurrentProbingPath { get; set; }
        /// <summary>
        /// stores the private bin of the application
        /// </summary>
        string PrivateBinFolderName { get; set; }

    }


    /// <summary>
    /// Defines a contract that provides human readable information about a member of code.
    /// </summary>
    public interface IHumanReadableMember
    {
        /// <summary>
        /// Name of the code member
        /// </summary>
        string MemberName { get; set; }

        /// <summary>
        /// Alias for the code member
        /// </summary>
        string MemberAlias { get; set; }

        /// <summary>
        /// Description
        /// </summary>
        string MemberDescription { get; set; }

        /// <summary>
        /// Identifier for code member
        /// </summary>
        string MemberID { get; set; }

    }


}
