using CommonServiceInterfaces;
using ComponentInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace ComponentManagerService
{
    public abstract class ComponentManagerBase<AdapterType, ConstructionDataType> : IComponentManagerBase<AdapterType, ConstructionDataType>
        where AdapterType : class
        where ConstructionDataType : class, IComponentConstructionData
    {
        private ComponentManagerBase() { }

        protected ComponentManagerBase(
            ICodeContractService codeContractDependency,
            IComponentCollection<ConstructionDataType> componentCollectionDependency,
            IComponentPersistence componentPersistenceDependency,
            IGenericInjectionFactory componentFactoryDependency,
            ITypeManipulator typeManipulatorDependency,
            ConstructionDataType constructionDataDependency,
            IComponentDataModel<AdapterType, ConstructionDataType> componentDataModelDependency,
            IServiceProvider serviceProviderDep)
        {
            string failPrefix = "Failed in ctor for: " + nameof(ComponentManagerBase<AdapterType, ConstructionDataType>) + ". ";

            _serviceProviderDep = serviceProviderDep ?? throw new ArgumentNullException(nameof(serviceProviderDep), failPrefix + "serviceProviderDep dependency cannot be null");
            _codeContractDependency = codeContractDependency ?? throw new ArgumentNullException(nameof(codeContractDependency), failPrefix + "Code contract dependency cannot be null");
            _componentCollectionDependency = componentCollectionDependency ?? throw new ArgumentNullException(nameof(componentCollectionDependency), failPrefix + "Component collection dependency cannot be null");
            _componentPersistenceDependency = componentPersistenceDependency ?? throw new ArgumentNullException(nameof(componentPersistenceDependency), failPrefix + "componentPersistenceDependency dependency cannot be null");
            _componentFactoryDependency = componentFactoryDependency ?? throw new ArgumentNullException(nameof(componentFactoryDependency), failPrefix + "componentFactoryDependency dependency cannot be null");
            _typeManipulatorDependency = typeManipulatorDependency ?? throw new ArgumentNullException(nameof(codeContractDependency), failPrefix + "typeManipulatorDependency dependency cannot be null");
            _constructionDataDependency = constructionDataDependency ?? throw new ArgumentNullException(nameof(constructionDataDependency), failPrefix + "constructionDataDependency dependency cannot be null");
            _componentDataModelDependency = componentDataModelDependency ?? throw new ArgumentNullException(nameof(codeContractDependency), failPrefix + "componentDataModelDependency dependency cannot be null");

        }

        protected readonly IServiceProvider _serviceProviderDep;
        protected readonly ICodeContractService _codeContractDependency;
        protected readonly IComponentPersistence _componentPersistenceDependency;
        protected readonly IGenericInjectionFactory _componentFactoryDependency;
        protected readonly ITypeManipulator _typeManipulatorDependency;
        protected readonly ConstructionDataType _constructionDataDependency;
        protected readonly IComponentDataModel<AdapterType, ConstructionDataType> _componentDataModelDependency;
        private IComponentCollection<ConstructionDataType> _componentCollectionDependency;

        //push all profile xml to appdata folder 
        public string ConstructionDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ComponentConstructionData");
        public const string ComponentConstructionProfileName = "ComponentManagerAppProfile";


        #region COMPONENT_CONSTRUCTION


        public IComponentCollection<ConstructionDataType> GetConstructionDataProfiles()
        {

            //if directory doesnt exist
            if (!Directory.Exists(ConstructionDataDirectory))
            {
                //create it 
                Directory.CreateDirectory(ConstructionDataDirectory);

            }

            //get all profile names tht match requested
            var requestedProfiles = _componentPersistenceDependency.GetAvailableComponentProfiles(ConstructionDataDirectory)?.Where(p => p.Equals(ComponentConstructionProfileName, StringComparison.Ordinal));

            //if any were found
            if (requestedProfiles != null && requestedProfiles.Count() > 0)
            {
                //ensure just one found
                _codeContractDependency.Requires<ArgumentOutOfRangeException>(requestedProfiles.Count() == 1, "Failed to find exactly one file for component manager profile: " + ComponentConstructionProfileName);
                //load that profile 
                _componentCollectionDependency = (IComponentCollection<ConstructionDataType>)_componentPersistenceDependency.LoadComponentState(_componentCollectionDependency.GetType(), ConstructionDataDirectory, ComponentConstructionProfileName);
            }

            //ensure instance is returned
            _codeContractDependency.Requires<ArgumentNullException>(_componentCollectionDependency != null, "Failed to load component manager profile: " + ComponentConstructionProfileName);

            //return dictionary
            return _componentCollectionDependency;
        }
        public void PersistConstructionDataCollection()
        {
            //persist current state
            _componentPersistenceDependency.PersistComponentState(_componentCollectionDependency, ConstructionDataDirectory, ComponentConstructionProfileName);
        }

        #endregion


        #region COMPONENT_PROFILE

        #region COMPONENT
        public void CreateComponent(string componentID, string createdBy = "Unknown")
        {
            #region preconditions
            //comp Id should not be null or empty
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to create component. Component ID cannot be empty");

            //try get construction dictionaries
            var constructionDataCollection = GetConstructionDataProfiles();
            //if any comps
            if (constructionDataCollection != null && constructionDataCollection.Count() > 0)
            {
                //try get construction data for comp id 
                bool ctorDataExists = constructionDataCollection.Any(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal));
                //ensure no construction data exists for that id
                _codeContractDependency.Requires<AmbiguousMatchException>(!ctorDataExists, "Failed to create component. A component with the name: " + componentID + " already exists");
            }

            #endregion

            //build new component profile path
            string adapterProfileDirectory = Path.Combine(ConstructionDataDirectory, componentID);

            //if directory already exists for this component
            if (Directory.Exists(adapterProfileDirectory))
            {
                //get directory
                DirectoryInfo directory = new DirectoryInfo(adapterProfileDirectory);
                //empty directory
                directory.DeleteAllFilesAndSubdirectories();
                //delete directory
                Directory.Delete(adapterProfileDirectory);
            }

            string defaultAdapterProfileName = "Default";

            //create new construction profile object with default adapter profile name
            _constructionDataDependency.ComponentID = componentID;
            _constructionDataDependency.DateCreated = DateTime.Now;
            _constructionDataDependency.DateModified = DateTime.Now;
            _constructionDataDependency.CreatedBy = createdBy;
            _constructionDataDependency.ModifiedBy = createdBy;
            _constructionDataDependency.IsActive = false;
            _constructionDataDependency.AdapterProfileDirectory = adapterProfileDirectory;
            _constructionDataDependency.AdapterProfileName = defaultAdapterProfileName;

            //add it
            bool isAdded = constructionDataCollection.AddComponent(_constructionDataDependency);
            //ensure added 
            _codeContractDependency.Requires<AmbiguousMatchException>(isAdded, "Failed to create component. Failed to add to collection object");
            //persist new construction info obj
            PersistConstructionDataCollection();

            #region postconditions

            //reload profiles and try get default
            var _newConstructionDataProfile = GetConstructionDataProfiles()?.FirstOrDefault(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal) && c.AdapterProfileName.Equals(defaultAdapterProfileName, StringComparison.Ordinal));
            //ensure any profiles found
            _codeContractDependency.Requires<FileNotFoundException>(_newConstructionDataProfile != null, "Failed to create component. Component seemed to be created and added, but was not found when re-loaded");

            #endregion

            //create adpater profile directory
            Directory.CreateDirectory(adapterProfileDirectory);

        }
        public void RenameComponent(string oldComponentID, string newComponentID, string updatedBy = "Unknown")
        {

            #region preconditions

            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(oldComponentID), "Failed to re-name component. No previous component ID was found");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(newComponentID), "Failed to re-name the component. The new ID cannot be empty");

            //try get ctor data collection
            var constructionDataCollection = GetConstructionDataProfiles();
            //try get old comp
            var oldComps = constructionDataCollection?.Where(c => c.ComponentID.Equals(oldComponentID, StringComparison.Ordinal));
            //ensure any are found
            _codeContractDependency.Requires<FileNotFoundException>(oldComps != null && oldComps.Count() > 0, "Failed to re-name component. component: " + oldComponentID + " not found.");
            //try get new comp
            var newComp = constructionDataCollection?.FirstOrDefault(c => c.ComponentID.Equals(newComponentID, StringComparison.Ordinal));
            //ensure none were found
            _codeContractDependency.Requires<AmbiguousMatchException>(newComp == null, "Failed to re-name component. The new name: " + newComponentID + " is already in use.");
            //build new component profile path
            string newComponentProfileDirectory = Path.Combine(ConstructionDataDirectory, newComponentID);
            //build old comp profile
            string oldComponentProfileDirectory = Path.Combine(ConstructionDataDirectory, oldComponentID);
            //ensure new comp directory doesnt exist
            _codeContractDependency.Requires<AmbiguousMatchException>(!Directory.Exists(newComponentProfileDirectory), "Failed to re-name component. The new name: " + newComponentID + " is already in use or a directory for it already exists.");

            #endregion


            //create new directory
            Directory.CreateDirectory(newComponentProfileDirectory);

            //if old directory exists
            if (Directory.Exists(oldComponentProfileDirectory))
            {
                //get paths to all files in old directory
                var oldAdapterProfilePaths = Directory.GetFiles(oldComponentProfileDirectory, "*.*", SearchOption.AllDirectories);

                //try to copy files over to new location
                try
                {
                    //iterate over old file paths
                    foreach (var adapterProfilePath in oldAdapterProfilePaths)
                    {
                        //get old file name from path
                        string fileName = Path.GetFileName(adapterProfilePath);
                        //get new path for profile
                        string newPath = Path.Combine(newComponentProfileDirectory, fileName);
                        //copy file to new location
                        File.Copy(adapterProfilePath, newPath);
                    }
                }
                //if anything goes wrong
                catch
                {
                    //if new directory exists
                    if (Directory.Exists(newComponentProfileDirectory))
                    {

                        //delete it and contents
                        Directory.Delete(newComponentProfileDirectory, true);
                    }

                    //rethrow
                    throw;
                }
            }

            //iterate over old comp construction data
            foreach (var comp in oldComps)
            {
                //update adapter profile directory
                comp.AdapterProfileDirectory = newComponentProfileDirectory;
                //update comp id 
                comp.ComponentID = newComponentID;
                //add date info
                comp.DateModified = DateTime.Now;
                //add udpated by
                comp.ModifiedBy = updatedBy;
            }

            //persist new state of ctor data
            PersistConstructionDataCollection();

            //delete old directory and contents
            Directory.Delete(oldComponentProfileDirectory, true);

        }
        public void DeleteComponent(string componentID)
        {
            //get construction profiles
            var constructionDataCollection = GetConstructionDataProfiles();
            //get construction data for comp id 
            var existingConstructionDataProfiles = constructionDataCollection?.Where(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal));
            //ensure any construction data exists for that id
            _codeContractDependency.Requires<FileNotFoundException>(existingConstructionDataProfiles != null && constructionDataCollection.Count() > 0, "Failed to delete component. Component with id: " + componentID + " not found");


            //iterate over profiles, have to toList it because hashset is readonly
            foreach (var profile in existingConstructionDataProfiles.ToList())
            {
                //remove from collection
                constructionDataCollection.RemoveComponent(profile);
            }

            //persist collection state
            PersistConstructionDataCollection();

            //init adapter profile dir
            string adapterProfileDirectory = Path.Combine(ConstructionDataDirectory, componentID);

            //delete directory
            Directory.Delete(adapterProfileDirectory, true);

            #region postconditions

            //reload profiles
            var constructionDataProfile = GetConstructionDataProfiles()?.FirstOrDefault(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal));
            //ensure no construction data exists for that id
            _codeContractDependency.Requires<FileNotFoundException>(constructionDataProfile == null, "Failed to delete component. Seemed to delete component: " + componentID + " but was still found when re-loaded");

            #endregion

        }
        #endregion

        #region ADAPTER
        public AdapterType GetAdapter(string assemblyPath, string className)
        {

            #region preconditions
            //ensure non empty inputs
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(assemblyPath), "Failed to get component adapter. Assembly path cannot be empty.");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(className), "Failed to get component adapter. Class name cannot be empty");

            #endregion


            //create instance of requested type
            var emptyProfile =
                (AdapterType)_componentFactoryDependency
                .Construct(
                    assemblyPath: assemblyPath,
                    className: className,
                    serviceProviderDep: _serviceProviderDep,
                    targetType: typeof(AdapterType));

            #region postconditions
            //ensure non null empty profile
            _codeContractDependency.Requires<NullReferenceException>(emptyProfile != null, "Failed to get component adapter. Failed to dynamically load type.");
            #endregion

            return emptyProfile;
        }
        #endregion

        #region PROFILE

        public IComponentDataModel<AdapterType, ConstructionDataType> GetComponentProfile(string componentID, string profileName)
        {
            //get adapter ctor data
            ConstructionDataType constructionDataProfile = GetComponentConstructionData(componentID, profileName);
            //get adapter
            AdapterType componentProfile = GetComponentAdapterHelper(constructionDataProfile, false);

            //create comp data model
            var comp = _componentDataModelDependency;
            _componentDataModelDependency.ComponentAdapterProfile = componentProfile;
            _componentDataModelDependency.ConstructionData = constructionDataProfile;

            //return requested construction info
            return comp;
        }
        public void RenameComponentProfile(string componentID, string oldProfileName, string newProfileName, string updatedBy = "Unknown")
        {
            #region preconditions

            string failurePrefix = "Failed to re-name component. ";
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), failurePrefix + "Component ID cannot be null");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(oldProfileName), failurePrefix + "old profile name cannot be null");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(newProfileName), failurePrefix + "new profile name cannot be null");


            //try get ctor data collection
            var constructionDataCollection = GetConstructionDataProfiles();
            //try get old comp profile
            var comps = constructionDataCollection?.Where(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal) && c.AdapterProfileName.Equals(oldProfileName, StringComparison.Ordinal));
            //ensure any are found
            _codeContractDependency.Requires<FileNotFoundException>(comps != null && comps.Count() > 0, failurePrefix + "Profile: " + oldProfileName + " for component: " + componentID + " not found.");
            //ensure single found
            _codeContractDependency.Requires<AmbiguousMatchException>(comps.Count() == 1, failurePrefix + "More than one item was found for Profile: " + oldProfileName + " and component: " + componentID);
            //try get new comp profile
            var newComp = constructionDataCollection?.FirstOrDefault(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal) && c.AdapterProfileName.Equals(newProfileName, StringComparison.Ordinal));
            //ensure none were found
            _codeContractDependency.Requires<AmbiguousMatchException>(newComp == null, failurePrefix + "The new name: " + newProfileName + " is already in use for component: " + componentID);


            #endregion

            //get single
            var comp = comps.Single();
            //create new
            CreateComponentProfile(componentID: componentID, adapterProfileName: newProfileName, createdBy: comp.CreatedBy);
            //get current adapter
            var adapter = GetComponentAdapterHelper(constructionData: comp, throwIfMissing: false);
            //rename current
            comp.AdapterProfileName = newProfileName;
            //update new with old adapter
            UpdateComponentProfile(adapterProfile: adapter, constructionProfile: comp);
            //delete old
            DeleteComponentProfile(componentID: componentID, adapterProfileName: oldProfileName);
            //persist component construction info
            PersistConstructionDataCollection();

        }

        public ConstructionDataType GetComponentConstructionData(string componentID, string profileName)
        {
            #region preconditions
            //ensure non null componentId
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to get component metadata. Component ID cannot be empty.");
            //ensure non null profile name
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(profileName), "Failed to get component metadata. Profile name cannot be empty");
            //get ctor data collection
            var componentsConstructionDataCollection = GetConstructionDataProfiles();
            //try to get the requested profile
            var constructionDataProfile = componentsConstructionDataCollection?.Where(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal) && c.AdapterProfileName.Equals(profileName, StringComparison.Ordinal));
            //ensure component has the requested profile
            _codeContractDependency.Requires<FileNotFoundException>(constructionDataProfile != null && constructionDataProfile.Count() > 0, "Failed to get component metadata. Unable to find profile: " + profileName);
            _codeContractDependency.Requires<AmbiguousMatchException>(constructionDataProfile.Count() == 1, "Failed to get component metadata. Failed to find exactly one profile with name: " + profileName);
            #endregion

            return constructionDataProfile.Single();
        }

        public AdapterType GetComponentAdapter(string componentID, string profileName)
        {
            //get adapter ctor data
            ConstructionDataType constructionDataProfile = GetComponentConstructionData(componentID, profileName);
            //return adapter
            return GetComponentAdapterHelper(constructionDataProfile, true);
        }
        protected virtual AdapterType GetComponentAdapterHelper(ConstructionDataType constructionData, bool throwIfMissing)
        {

            //init profile to null
            AdapterType componentProfile = null;

            _codeContractDependency.Requires<ArgumentNullException>(constructionData != null, "Failed to get component adapter. Construction data argument cannot be null");

            //if we want to throw on missing profiles
            if (throwIfMissing)
            {
                //check for asm info
                _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(constructionData.AssemblyPath), "Failed to get component adapter. Assembly path cannot be empty");
                _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(constructionData.ClassName), "Failed to get component adapter. Profile name cannot be empty");
            }

            //if a class name and asm are registered
            if (!string.IsNullOrEmpty(constructionData.AssemblyPath) && !string.IsNullOrEmpty(constructionData.ClassName))
            {
                //attempt to construct and load adapter
                try
                {
                    //create mock instance of component
                    AdapterType mockCompInstance =
                        (AdapterType)_componentFactoryDependency
                        .Construct(
                            assemblyPath: constructionData.AssemblyPath,
                            className: constructionData.ClassName,
                            serviceProviderDep: _serviceProviderDep,
                            targetType: typeof(AdapterType));

                    //load comp state
                    componentProfile =
                        (AdapterType)_componentPersistenceDependency
                        .LoadComponentState(
                            mockCompInstance.GetType(),
                            constructionData.AdapterProfileDirectory,
                            constructionData.AdapterProfileName);
                }
                //catch any errors
                catch (Exception)
                {
                    //rethrow if specified
                    if (throwIfMissing)
                    {
                        throw;
                    }
                }
            }

            //ensure we got an adapter if we care
            _codeContractDependency.Requires<MissingMemberException>(!throwIfMissing || componentProfile != null, "Failed to get component adapter. Seemed to get adapter successfully but adapter object still null");

            return componentProfile;
        }
        public void CreateComponentProfile(string componentID, string adapterProfileName, string createdBy = "Unknown")
        {
            #region preconditions
            //ensure non empty inputs
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to create component profile. Component ID cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(adapterProfileName), "Failed to create component profile. Profile name cannot be empty");

            //get ctor data collection
            var componentsConstructionDataCollection = GetConstructionDataProfiles();
            //try to get profile that matches
            var profile = componentsConstructionDataCollection.FirstOrDefault(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal));
            //ensure comp exists
            _codeContractDependency.Requires<FileNotFoundException>(profile != null, "Failed to create component profile. Component: " + componentID + " was not found");
            //try get profile
            var profileExists = componentsConstructionDataCollection.Any(c => c.AdapterProfileName.Equals(adapterProfileName, StringComparison.Ordinal));
            //ensure profile doesnt already exist
            _codeContractDependency.Requires<AmbiguousMatchException>(!profileExists, "Failed to create component profile. Component profile: " + adapterProfileName + " already exists");
            #endregion

            //build new component profile path
            string componentProfileDirectory = Path.Combine(ConstructionDataDirectory, componentID);

            //create new construction profile object with adapter profile name
            var newConstructionDataProfile = _constructionDataDependency;
            newConstructionDataProfile.ComponentID = componentID;
            newConstructionDataProfile.DateCreated = DateTime.Now;
            newConstructionDataProfile.DateModified = DateTime.Now;
            newConstructionDataProfile.CreatedBy = createdBy;
            newConstructionDataProfile.ModifiedBy = createdBy;
            newConstructionDataProfile.IsActive = false;
            newConstructionDataProfile.AdapterProfileDirectory = componentProfileDirectory;
            newConstructionDataProfile.AdapterProfileName = adapterProfileName;

            //add it
            bool isAdded = componentsConstructionDataCollection.AddComponent(newConstructionDataProfile);
            //ensure added
            _codeContractDependency.Requires<AmbiguousMatchException>(isAdded, "Failed to create component adapter. Failed to add newly created component profile to collection");
            //persist new construction info obj
            PersistConstructionDataCollection();

            #region postconditions

            //reload profiles and try get new
            var _newConstructionDataProfile = GetConstructionDataProfiles()?.FirstOrDefault(p => p.ComponentID.Equals(componentID, StringComparison.Ordinal) && p.AdapterProfileName.Equals(adapterProfileName, StringComparison.Ordinal));
            //ensure new one exists
            _codeContractDependency.Requires<FileNotFoundException>(_newConstructionDataProfile != null, "Failed to create component adapter. Seemed to create and add profile but was unable to re-load it");

            #endregion

        }
        public void UpdateComponentProfile(AdapterType adapterProfile, ConstructionDataType constructionProfile)
        {
            #region preconditions

            #region ComponentProfileChecks

            //ensure non null profile data
            _codeContractDependency.Requires<ArgumentNullException>(adapterProfile != null, "Failed to update component profile. No profile exists for this component");

            #endregion

            #region ConstructionDataChecks

            //construction data should be non null
            _codeContractDependency.Requires<ArgumentNullException>(constructionProfile != null, "Failed to update component profile. Construction data cannot be null");
            //componentID should be non null
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(constructionProfile.ComponentID), "Failed to update component profile. Coponent ID cannot be empty");
            //ensure non null profile name
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(constructionProfile.AdapterProfileName), "Failed to update component profile. Profile name cannot be empty");
            //ensure non null assembly path
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(constructionProfile.AssemblyPath), "Failed to update component profile. Assembly path cannot be empty");
            //ensure non null class name
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(constructionProfile.ClassName), "Failed to update component profile. Class name cannot be empty");
            //ensure type selected is derived from generic type arg ComponentType
            _codeContractDependency.Requires<InvalidCastException>(typeof(AdapterType).IsAssignableFrom(adapterProfile.GetType()), "Failed to update component profile. Adapter type did not implement required interface: " + typeof(AdapterType).ToString());
            //get construction dictionaries
            var constructionDataCollection = GetConstructionDataProfiles();
            //get construction data for comp id 
            var existingConstructionDataProfiles = constructionDataCollection?.Where(c => c.ComponentID.Equals(constructionProfile.ComponentID, StringComparison.Ordinal) && c.AdapterProfileName.Equals(constructionProfile.AdapterProfileName, StringComparison.Ordinal));
            //ensure any construction data exists for that id
            _codeContractDependency.Requires<FileNotFoundException>(existingConstructionDataProfiles != null && existingConstructionDataProfiles.Count() > 0, "Failed to update component profile. No metadata exists for component");
            //ensure one
            _codeContractDependency.Requires<AmbiguousMatchException>(existingConstructionDataProfiles.Count() == 1, "Failed to update component profile. Failed to find exactly one matching component profile");

            #endregion



            #endregion

            //get single construction profile 
            var existingConstructionDataProfile = existingConstructionDataProfiles.Single();

            //add date updated info
            constructionProfile.DateModified = DateTime.Now;
            //copy over necessary old info
            constructionProfile.AdapterProfileDirectory = existingConstructionDataProfile.AdapterProfileDirectory;
            constructionProfile.DateCreated = existingConstructionDataProfile.DateCreated;
            constructionProfile.CreatedBy = existingConstructionDataProfile.CreatedBy;
            //remove old
            constructionDataCollection.RemoveComponent(existingConstructionDataProfile);
            //add new
            bool isAdded = constructionDataCollection.AddComponent(constructionProfile);
            //ensure added
            _codeContractDependency.Requires<AmbiguousMatchException>(isAdded, "Failed to update component profile. Failed to add updated component profile.");

            //if updated construction data profile is now the active profile
            if (constructionProfile.IsActive)
            {
                //get all other active profiles for this component ID(should only be one)
                var activeProfiles = constructionDataCollection.Where(c => c.IsActive && c.ComponentID.Equals(constructionProfile.ComponentID, StringComparison.Ordinal) && !c.AdapterProfileName.Equals(constructionProfile.AdapterProfileName, StringComparison.Ordinal));

                //iterate over active profiles
                foreach (var profile in activeProfiles)
                {
                    //set profiles to inactive
                    profile.IsActive = false;
                }
            }

            //persist new construction info obj
            PersistConstructionDataCollection();
            //persist component profile once construction data profile is persisted
            _componentPersistenceDependency.PersistComponentState(adapterProfile, constructionProfile.AdapterProfileDirectory, constructionProfile.AdapterProfileName);

            #region postconditions

            //reload profiles and try get updated
            var _updatedConstructionDataProfile = GetConstructionDataProfiles()?.FirstOrDefault(p => p.ComponentID.Equals(constructionProfile.ComponentID, StringComparison.Ordinal) && p.AdapterProfileName.Equals(constructionProfile.AdapterProfileName, StringComparison.Ordinal));
            //ensure new one exists
            _codeContractDependency.Requires<FileNotFoundException>(_updatedConstructionDataProfile != null, "Failed to update component profile. Seemed to update profile successfully but re-load it");

            #endregion


        }
        public void DeleteComponentProfile(string componentID, string adapterProfileName)
        {

            #region preconditions
            //ensure non null componentId
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to delete profile. Component ID cannot be empty");
            //ensure non null profileName
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(adapterProfileName), "Failed to delete profile. Profile name cannot be empty");
            //get all consturciton dictionaries
            var componentsConstructionDataCollection = GetConstructionDataProfiles();
            //try get all requested profiles
            var constructionDataProfile = componentsConstructionDataCollection.FirstOrDefault(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal) && c.AdapterProfileName.Equals(adapterProfileName, StringComparison.Ordinal));
            //ensure any
            _codeContractDependency.Requires<FileNotFoundException>(constructionDataProfile != null, "Failed to delete profile. No profile metadata found");
            #endregion

            //remove the component profile
            _componentPersistenceDependency.DeleteComponentState(constructionDataProfile.AdapterProfileDirectory, constructionDataProfile.AdapterProfileName);
            //remove construction data profile
            componentsConstructionDataCollection.RemoveComponent(constructionDataProfile);

            //persist new construction collection state
            PersistConstructionDataCollection();

            #region postcoditions

            //reload profiles
            var _constructionDataProfile = GetConstructionDataProfiles()?.FirstOrDefault(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal) && c.AdapterProfileName.Equals(adapterProfileName, StringComparison.Ordinal));
            //ensure no construction data exists for that id
            _codeContractDependency.Requires<FileNotFoundException>(_constructionDataProfile == null, "Failed to delete profile. Seemed to delete successfully but profile still found");

            #endregion

        }
        public IComponentDataModel<AdapterType, ConstructionDataType> GetActiveComponentProfile(string componentID)
        {

            //get active construction info
            ConstructionDataType constructionData = GetActiveComponentConstructionData(componentID);
            //get active adapter
            AdapterType componentProfile = GetActiveComponentAdapterHelper(constructionData, false);

            //init comp data model
            var comp = _componentDataModelDependency;
            _componentDataModelDependency.ConstructionData = constructionData;
            _componentDataModelDependency.ComponentAdapterProfile = componentProfile;

            //return requested comp model
            return comp;
        }
        public ConstructionDataType GetActiveComponentConstructionData(string componentID)
        {

            #region preconditions
            //ensure non null componentId
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to get active component metadata. Component ID cannot be empty");
            //get all consturciton dictionaries
            var componentsConstructionDataCollection = GetConstructionDataProfiles();
            //init construction data profile
            List<ConstructionDataType> constructionDataProfiles;
            //try to get the active ctor data for requested comp ID
            constructionDataProfiles = componentsConstructionDataCollection.Where(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal) && c.IsActive)?.ToList();
            //ensure component has any profiles
            _codeContractDependency.Requires<ArgumentOutOfRangeException>(constructionDataProfiles != null && constructionDataProfiles.Count() > 0, "Failed to get active component metadata. No profiles exists for this component");
            //ensure only one was found
            _codeContractDependency.Requires<AmbiguousMatchException>(constructionDataProfiles.Count() == 1, "Failed to get active component metadata. Failed to find exactly one matching active profile");
            #endregion
            //return single active
            return constructionDataProfiles.Single();

        }
        public AdapterType GetActiveComponentAdapter(string componentID)
        {
            //get active ctor data
            ConstructionDataType activeConstructionDataProfile = GetActiveComponentConstructionData(componentID);
            //get adapter profile
            return GetActiveComponentAdapterHelper(activeConstructionDataProfile, true);
        }
        protected virtual AdapterType GetActiveComponentAdapterHelper(ConstructionDataType constructionData, bool throwIfMissing)
        {
            AdapterType componentProfile = null;

            _codeContractDependency.Requires<ArgumentNullException>(constructionData != null, "Failed to get active adapter profile. Construction data cannot be null");

            //if we want to throw on missing profiles
            if (throwIfMissing)
            {
                //check for asm info
                _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(constructionData.AssemblyPath), "Failed to get active adapter profile. Assembly path cannot be empty");
                _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(constructionData.ClassName), "Failed to get active adapter profile. Class name cannot be empty");
            }

            //create mock instance of component
            AdapterType mockCompInstance =
                (AdapterType)_componentFactoryDependency
                .Construct(
                    assemblyPath: constructionData.AssemblyPath,
                    className: constructionData.ClassName,
                    serviceProviderDep: _serviceProviderDep,
                    targetType: typeof(AdapterType));

            //load comp state
            componentProfile = (AdapterType)_componentPersistenceDependency.LoadComponentState(mockCompInstance.GetType(), constructionData.AdapterProfileDirectory, constructionData.AdapterProfileName);


            _codeContractDependency.Requires<MissingMemberException>(componentProfile != null, "Failed to get active adapter profile. No profile found");


            return componentProfile;
        }
        public void SetActiveComponentProfile(string componentID, string profileName)
        {
            #region preconditions
            //ensure non null componentId
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to set active adapter profile. Component ID cannot be empty");
            //ensure non null profileName
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(profileName), "Failed to set active adapter profile. Profile name cannot be empty");
            #endregion

            var constructionProfile = GetComponentConstructionData(componentID, profileName);
            var adapterProfile = GetComponentAdapterHelper(constructionProfile, true);
            //set it to active
            constructionProfile.IsActive = true;
            //update that construction profile
            UpdateComponentProfile(adapterProfile, constructionProfile);

        }
        public IComponentCollection<ConstructionDataType> GetComponentProfiles(string componentID)
        {

            #region preconditions
            //ensure non null componentId
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to get component profiles. Component ID cannot be empty");
            //get all consturciton data profiles
            var componentsConstructionDataCollection = GetConstructionDataProfiles();
            //get profiles by comp ID
            IComponentCollection<ConstructionDataType> IComponentConstructionDataCollectionByCompID =
                (IComponentCollection<ConstructionDataType>)componentsConstructionDataCollection.Where(c => c.ComponentID.Equals(componentID, StringComparison.Ordinal));

            //ensure component has any 
            _codeContractDependency.Requires<ArgumentOutOfRangeException>(IComponentConstructionDataCollectionByCompID != null && IComponentConstructionDataCollectionByCompID.Count() > 0, "Failed to get component profiles. None exist for this component");
            #endregion

            //return all profiles
            return IComponentConstructionDataCollectionByCompID;
        }
        public IEnumerable<string> GetComponentProfileNames(string componentID)
        {
            #region preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to get component profile names. Component ID cannot be empty");
            #endregion

            //get all profile names
            List<string> profileNames = GetComponentProfiles(componentID).Select(c => c.AdapterProfileName).ToList();
            //return profile names
            return profileNames;
        }

        #endregion

        #endregion

        #region METHODS

        public IEnumerable<MethodInfo> GetComponentProfileMethods(string componentID, string profileName = "")
        {

            #region preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to get component profile methods. Component Id cannot be empty");
            #endregion

            //get component
            var component =
                string.IsNullOrEmpty(profileName) ?
                GetActiveComponentAdapter(componentID)
                :
                GetComponentAdapter(componentID, profileName);

            //ensure component non null
            _codeContractDependency.Requires<ArgumentNullException>(component != null, "Failed to get component profile methods. Failed to load profile");

            //get methods
            IEnumerable<MethodInfo> componentMethods = GetComponentProfileMethods(component);

            //return list
            return componentMethods;

        }
        public IEnumerable<MethodInfo> GetComponentProfileMethods(object componentAdapterProfile)
        {
            #region preconditions
            _codeContractDependency.Requires<ArgumentOutOfRangeException>(componentAdapterProfile != null, "Failed to get component profile methods. Profile name cannot be empty");
            #endregion

            //get methods
            var componentMethods =
                componentAdapterProfile
                .GetType()//get comp type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance);//get public instance methods

            //return list
            return componentMethods;

        }
        public IEnumerable<MethodInfo> GetComponentProfileMethods(string componentID, string methodName = "", string profileName = "")
        {

            #region preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to get component profile methods. Component ID cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(methodName), "Failed to get component profile methods. Method name cannot be empty");
            #endregion

            //get component
            var component = string.IsNullOrEmpty(profileName) ?
                GetActiveComponentAdapter(componentID)
                :
                GetComponentAdapter(componentID, profileName);

            //ensure component non null
            _codeContractDependency.Requires<FileNotFoundException>(component != null, "Failed to get component profile methods. Component failed to load");
            //init output methods
            IEnumerable<MethodInfo> componentFilteredMethods;

            //if no method name supplied
            if (string.IsNullOrEmpty(methodName))
            {
                //get all
                componentFilteredMethods = GetComponentProfileMethods(component);
            }
            else
            {
                //filter by name provided
                componentFilteredMethods = GetComponentProfileMethods(component)?.Where(m => m.Name.Equals(methodName, StringComparison.Ordinal));
            }

            //return list
            return componentFilteredMethods;

        }

        #endregion

        #region COMPONENT_ACTION

        public IEnumerable<MethodInfo> GetComponentProfileActions(string componentID, string actionID = "", string profileName = "")
        {

            #region preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to get component profile actions. Component ID cannot be empty");
            #endregion

            //get all methods
            IEnumerable<MethodInfo> methods = GetComponentProfileMethods(componentID, profileName);

            //IEnumerable<MethodInfo> actions = methods?.Where(m => m.IsDefined(typeof(IComponentActionAttribute)));
            //filter for methods marked as actions
            IEnumerable<MethodInfo> actions = methods?.Where(m => m.GetCustomAttributes().Any(a => typeof(IComponentActionAttribute).IsAssignableFrom(a.GetType())));

            //if action id supplied
            if (!string.IsNullOrEmpty(actionID))
            {
                //ensure any actions found
                _codeContractDependency.Requires<MissingMethodException>(actions != null && actions.Count() > 0, "Failed to get component profile actions. None exist");

                //get requested actions by id
                actions = actions
                    .Where(a =>
                    !string.IsNullOrEmpty((a.GetCustomAttributes()?.FirstOrDefault(at => typeof(IComponentActionAttribute).IsAssignableFrom(at.GetType())) as IComponentActionAttribute)?.MemberID)
                    &&
                    (a.GetCustomAttributes().FirstOrDefault(at => typeof(IComponentActionAttribute).IsAssignableFrom(at.GetType())) as IComponentActionAttribute).MemberID.Equals(actionID, StringComparison.Ordinal));

                //ensure any
                _codeContractDependency.Requires<MissingMethodException>(actions != null && actions.Count() > 0, "Failed to get component profile actions. None exist with the member id: " + actionID);
                //ensure just one 
                _codeContractDependency.Requires<AmbiguousMatchException>(actions.Count() == 1, "Failed to get component profile actions. Failed to find exactly one action with member id: " + actionID);
            }

            return actions;
        }
        public virtual object InvokeActionOnComponentProfile(string componentID, string actionID, Dictionary<string, string> actionArgs = null, string profileName = "")
        {

            #region preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to invoke action on component. Component Id cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionID), "Failed to invoke action on component. Action id cannot be empty");
            #endregion

            //get specified adapter profile or active
            var componentProfile =
                (string.IsNullOrEmpty(profileName)) ?
                GetActiveComponentProfile(componentID)
                :
                GetComponentProfile(componentID, profileName);

            _codeContractDependency.Requires<ArgumentNullException>(componentProfile != null, "Failed to invoke action on component. Component failed to load");
            _codeContractDependency.Requires<ArgumentNullException>(componentProfile.ComponentAdapterProfile != null, "Failed to invoke action on component. No profile exists for this component");
            _codeContractDependency.Requires<ArgumentNullException>(componentProfile.ConstructionData != null, "Failed to invoke action on component. No metadata exists for this component");
            //get all methods that match and are labeled as acitons(should only be one)
            IEnumerable<MethodInfo> componentActions = GetComponentProfileActions(componentID, actionID, profileName);
            //ensure action list is non zero
            _codeContractDependency.Requires<MissingMethodException>(componentActions != null, "Failed to invoke action on component. No actions exists in this component");
            //ensure only one was found
            _codeContractDependency.Requires<MissingMethodException>(componentActions.Count() == 1, "Failed to invoke action on component. Failed to match exactly one action with member id: " + actionID);
            //invoke action on comp
            object retVal = InvokeActionOnComponentProfile(componentProfile.ComponentAdapterProfile, componentActions.Single(), actionArgs, profileName);
            //update comp
            UpdateComponentProfile((AdapterType)componentProfile.ComponentAdapterProfile, (ConstructionDataType)componentProfile.ConstructionData);

            return retVal;

        }
        protected object InvokeActionOnComponentProfile(object componentAdapter, MethodInfo action, Dictionary<string, string> actionArgs, string profileName = "")
        {
            #region preconditions
            _codeContractDependency.Requires<ArgumentNullException>(componentAdapter != null, "Failed to invoke action on component. Component adapter cannot be null");
            _codeContractDependency.Requires<ArgumentNullException>(action != null, "Failed to invoke action on component. Action cannot be null");
            #endregion

            //get single method
            MethodInfo componentAction = action;
            //convert arg to expected types if args are supplied
            object[] convertedArgs = actionArgs == null || actionArgs.Count() == 0 ? null : _typeManipulatorDependency.ConvertArgsToExpectedTypes(actionArgs, componentAction);
            //invoke action on component. will throw exception if supplied args are incorrect
            object retVal = componentAction.Invoke(componentAdapter, convertedArgs);


            return retVal;
        }



        #endregion


    }

    public class OpenComponentManager<ComponentAdapterType, ConstructionDataType> : ComponentManagerBase<ComponentAdapterType, ConstructionDataType>, IComponentManager<ComponentAdapterType, ConstructionDataType>
        where ComponentAdapterType : class, IComponentAdapter
        where ConstructionDataType : class, IComponentConstructionData
    {

        //provides mechanism for locking device objects to prevent concurrent invokations
        //static to prevent concurrency issues across all instances
        private static readonly object DeviceLock = new object();

        protected OpenComponentManager(
            ICodeContractService codeContractDependency,
            IComponentCollection<ConstructionDataType> componentCollectionDependency,
            IComponentPersistence componentPersistenceDependency,
            IGenericInjectionFactory componentFactoryDependency,
            ITypeManipulator typeManipulatorDependency,
            ConstructionDataType IComponentConstructionDataDependency,
            IComponentDataModel<ComponentAdapterType, ConstructionDataType> componentDataModelDependency,
            IServiceProvider serviceProviderDep)
            :
            base(codeContractDependency,
                  componentCollectionDependency,
                  componentPersistenceDependency,
                  componentFactoryDependency,
                  typeManipulatorDependency,
                  IComponentConstructionDataDependency,
                  componentDataModelDependency,
                  serviceProviderDep)
        {

        }


        #region COMPONENT_ADAPTER

        protected override ComponentAdapterType GetActiveComponentAdapterHelper(ConstructionDataType constructionData, bool throwIfMissing)
        {
            var adapter = base.GetActiveComponentAdapterHelper(constructionData, throwIfMissing);

            adapter?.InjectServiceProvider(_serviceProviderDep);

            return adapter;
        }
        protected override ComponentAdapterType GetComponentAdapterHelper(ConstructionDataType constructionData, bool throwIfMissing)
        {
            var adapter = base.GetComponentAdapterHelper(constructionData, throwIfMissing);

            adapter?.InjectServiceProvider(_serviceProviderDep);

            return adapter;
        }


        #endregion


        #region COMPONENT_ACTION_METHODS


        public void InitializeComponentProfile(string componentID, string profileName = "")
        {
            #region preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to initialize component. Component ID cannot be empty");

            #endregion

            //get construction data for adapter
            var constructionData = string.IsNullOrEmpty(profileName) ?
                GetActiveComponentConstructionData(componentID)
                :
                GetComponentConstructionData(componentID, profileName);

            //ensure component profile non null
            _codeContractDependency.Requires<ArgumentNullException>(constructionData != null, "Failed to initialize component. No metadata exists for this component ");

            //get adapter object
            var adapter = GetComponentAdapterHelper(constructionData, throwIfMissing: true);

            //ensure component profile non null
            _codeContractDependency.Requires<ArgumentNullException>(adapter != null, "Failed to initialize component. No adapter profile exists for this component");

            //prevent concurrent call to device
            lock (DeviceLock)
            {

                try
                {
                    //connect
                    adapter.Connect();
                    //init the motor including stopping, resetting, committing values to component, and config testing
                    adapter.Initialize();
                    //Refresh state
                    adapter.ReadState();
                    //persist new state
                    UpdateComponentProfile(adapter, constructionData);
                }
                finally
                {
                    //if not null and connected, disconnect from device
                    adapter?.Dispose();
                }
            }

        }
        public object InitializeAndInvokeActionOnComponentProfile(string componentID, string actionID, Dictionary<string, string> actionArgs, string profileName = "")
        {

            #region preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to initialize and invoke from component manager. Component id cannot be null.");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionID), "Failed to initialize and invoke from component manager. Action ID cannot be null.");
            #endregion

            //get construction data for adapter
            var constructionData = string.IsNullOrEmpty(profileName) ?
                GetActiveComponentConstructionData(componentID)
                :
                GetComponentConstructionData(componentID, profileName);

            //ensure component profile non null
            _codeContractDependency.Requires<ArgumentNullException>(constructionData != null, "Failed to initialize and invoke from component manager. No metadata exists for this component");

            //get adapter object
            var adapter = GetComponentAdapterHelper(constructionData, throwIfMissing: true);

            //ensure component profile non null
            _codeContractDependency.Requires<ArgumentNullException>(adapter != null, "Failed to initialize and invoke from component manager. No adapter profile exists for this component");

            //prevent concurrent calls to device
            lock (DeviceLock)
            {
                try
                {
                    //get action
                    var action = GetComponentProfileActions(componentID, actionID, profileName).Single();
                    //connect
                    adapter.Connect();
                    //init the component including stopping, resetting, committing values to component, and config testing
                    adapter.Initialize();
                    //invoke dynamic command on component
                    object retVal = InvokeActionOnComponentProfile(adapter, action, actionArgs, profileName);
                    //Refresh state
                    adapter.ReadState();

                    UpdateComponentProfile(adapter, constructionData);

                    return retVal;
                }
                finally
                {
                    //if not null and connected, disconnect from device
                    adapter?.Dispose();
                }
            }

        }

        public object ConnectAndInvokeActionOnComponentProfile(string componentID, string actionID, Dictionary<string, string> actionArgs, string profileName = "")
        {

            #region preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to connect and invoke from component manager. Component ID cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionID), "Failed to connect and invoke from component manager. Action ID cannot be empty");
            #endregion

            //get construction data for adapter
            var constructionData = string.IsNullOrEmpty(profileName) ?
                GetActiveComponentConstructionData(componentID)
                :
                GetComponentConstructionData(componentID, profileName);

            //ensure component profile non null
            _codeContractDependency.Requires<ArgumentNullException>(constructionData != null, "Failed to connect and invoke from component manager. No metadata exists for this component");

            //get adapter object
            var adapter = GetComponentAdapterHelper(constructionData, throwIfMissing: true);

            //ensure component profile non null
            _codeContractDependency.Requires<ArgumentNullException>(adapter != null, "Failed to connect and invoke from component manager. No adapter profile exists for this component");

            //prevent concurrent calls to device
            lock (DeviceLock)
            {
                try
                {
                    //get action
                    var action = GetComponentProfileActions(componentID, actionID, profileName).Single();
                    //connect to device
                    adapter.Connect();
                    //invoke dynamic command on component
                    object retVal = InvokeActionOnComponentProfile(adapter, action, actionArgs, profileName);
                    //Refresh state
                    adapter.ReadState();

                    UpdateComponentProfile(adapter, constructionData);

                    return retVal;
                }
                finally
                {
                    //if not null and connected, disconnect from device
                    adapter?.Dispose();
                }
            }

        }

        public override object InvokeActionOnComponentProfile(string componentID, string actionID, Dictionary<string, string> actionArgs = null, string profileName = "")
        {
            #region preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to invoke action on component. Component ID cannot be empty");
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionID), "Failed to invoke action on component. Action ID cannot be empty");
            #endregion

            //get construction data for adapter
            var constructionData = string.IsNullOrEmpty(profileName) ?
                GetActiveComponentConstructionData(componentID)
                :
                GetComponentConstructionData(componentID, profileName);

            //ensure component profile non null
            _codeContractDependency.Requires<ArgumentNullException>(constructionData != null, "Failed to invoke action on component. No metadata exists for component");

            //get adapter object
            var adapter = GetComponentAdapterHelper(constructionData, throwIfMissing: true);

            //ensure component profile non null
            _codeContractDependency.Requires<ArgumentNullException>(adapter != null, "Failed to invoke action on component. No adapter profile exists for this component");

            //prevent concurrent calls to device
            lock (DeviceLock)
            {

                try
                {

                    //get all methods that match and are labeled as acitons(should only be one)
                    IEnumerable<MethodInfo> componentActions = GetComponentProfileActions(componentID, actionID, profileName);
                    //ensure action list is non zero
                    _codeContractDependency.Requires<MissingMethodException>(componentActions != null, "Failed to invoke action on component. No actions exists on this component profile");
                    //ensure only one was found
                    _codeContractDependency.Requires<MissingMethodException>(componentActions.Count() == 1, "Failed to invoke action on component. Failed to match exactly one action with member id: " + actionID);
                    //invoke action on comp
                    object retVal = InvokeActionOnComponentProfile(adapter, componentActions.Single(), actionArgs, profileName);
                    //Refresh state
                    //adapter.ReadState();
                    //update comp
                    UpdateComponentProfile(adapter, constructionData);

                    return retVal;
                }
                finally
                {
                    adapter?.Dispose();
                }
            }
            
        }

        #endregion

        public ComponentAdapterType GetComponentProfileStates(string componentID, string profileName = "")
        {
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(componentID), "Failed to get component state. Component Id cannot be empty");

            //get construction data for adapter
            var constructionData = string.IsNullOrEmpty(profileName) ?
                GetActiveComponentConstructionData(componentID)
                :
                GetComponentConstructionData(componentID, profileName);

            //ensure component profile non null
            _codeContractDependency.Requires<ArgumentNullException>(constructionData != null, "Failed to get component state. No metadata exists for this component");

            //get adapter object
            var adapter = GetComponentAdapterHelper(constructionData, throwIfMissing: true);

            //ensure component profile non null
            _codeContractDependency.Requires<ArgumentNullException>(adapter != null, "Failed to get component state. No adapter profile exists for this component");

            //prevent concurrent calls to device
            lock (DeviceLock)
            {
                try
                {
                    //connect to device
                    adapter.Connect();
                    //Refresh state
                    adapter.ReadState();

                    UpdateComponentProfile(adapter, constructionData);

                    return adapter;
                }
                finally
                {
                    //if not null and connected, disconnect from device
                    adapter?.Dispose();
                }
            }

        }

    }

    public class ComponentManager : OpenComponentManager<IComponentAdapter, IComponentConstructionData>
    {


        public ComponentManager(
            ICodeContractService codeContractDependency,
            IComponentCollection<IComponentConstructionData> componentCollectionDependency,
            IComponentPersistence componentPersistenceDependency,
            IGenericInjectionFactory componentFactoryDependency,
            ITypeManipulator typeManipulatorDependency,
            IComponentConstructionData IComponentConstructionDataDependency,
            IComponentDataModel<IComponentAdapter, IComponentConstructionData> componentDataModelDependency,
            IServiceProvider serviceProviderDep)
            :
            base(codeContractDependency,
                  componentCollectionDependency,
                  componentPersistenceDependency,
                  componentFactoryDependency,
                  typeManipulatorDependency,
                  IComponentConstructionDataDependency,
                  componentDataModelDependency,
                  serviceProviderDep)
        {

        }

    }

    [CollectionDataContract]
    public class ComponentConstructionCollection<ConstructionDataType> : HashSet<ConstructionDataType>, IComponentCollection<ConstructionDataType>
    where ConstructionDataType : class, IComponentConstructionData
    {

        protected ComponentConstructionCollection() : base(new ComponentConstructionDataComparer()) { }

        public ComponentConstructionCollection(IComponentEqualityComparer comparer) : base(comparer) { }

        public bool AddComponent(object item)
        {
            //ensure interface is assignable from runtime type
            if (!typeof(ConstructionDataType).IsAssignableFrom(item.GetType()))
                throw new InvalidCastException(typeof(ConstructionDataType).ToString() + " TO " + item.GetType().ToString());

            //call hashset add
            return Add((ConstructionDataType)item);
        }

        public bool RemoveComponent(object item)
        {
            //ensure interface is assignable from runtime type
            if (!typeof(ConstructionDataType).IsAssignableFrom(item.GetType()))
                throw new InvalidCastException(typeof(ConstructionDataType).ToString() + " TO " + item.GetType().ToString());

            //call hashset remove
            return Remove((ConstructionDataType)item);
        }
    }

    public class ComponentConstructionDataComparer : IComponentEqualityComparer
    {
        public ComponentConstructionDataComparer() { }

        public bool Equals(IComponentConstructionData x, IComponentConstructionData y)
        {
            bool isSame = x.ComponentID.Equals(y.ComponentID, StringComparison.Ordinal) && x.AdapterProfileName.Equals(y.AdapterProfileName, StringComparison.Ordinal);
            return isSame;

        }

        public int GetHashCode(IComponentConstructionData obj)
        {
            return StringComparer.Ordinal.GetHashCode(obj.AdapterProfileName) ^ StringComparer.Ordinal.GetHashCode(obj.ComponentID);
        }

    }

    [DataContract]
    public class ComponentDataModel<ComponentAdapterType, ConstructionDataType> : IComponentDataModel<ComponentAdapterType, ConstructionDataType>
    {
        public ComponentDataModel() { }

        [DataMember]
        public object ConstructionData
        {
            get
            {
                return constructionData;
            }
            set
            {
                constructionData = (ConstructionDataType)value;
            }
        }
        private ConstructionDataType constructionData;

        [DataMember]
        public object ComponentAdapterProfile
        {
            get
            {
                //return private var
                return componentAdapterProfile;
            }
            set
            {
                componentAdapterProfile = (ComponentAdapterType)value;
            }
        }
        private ComponentAdapterType componentAdapterProfile;
    }

    [DataContract]
    public class ComponentConstructionData : IComponentConstructionData
    {
        [DataMember]
        [Display]
        public string ComponentID { get; set; }
        [DataMember]
        [Display]
        public string AdapterProfileName { get; set; }
        [DataMember]
        [Display]
        public string AdapterProfileDirectory { get; set; }
        [DataMember]
        [Display]
        public bool IsActive { get; set; }
        [DataMember]
        [Display]
        public DateTime DateCreated { get; set; }
        [DataMember]
        [Display]
        public string CreatedBy { get; set; }
        [DataMember]
        [Display]
        public DateTime DateModified { get; set; }
        [DataMember]
        [Display]
        public string ModifiedBy { get; set; }
        [DataMember]
        [Display]
        public string AssemblyPath { get; set; }
        [DataMember]
        [Display]
        public string ClassName { get; set; }
    }


    public static class DirectoryExtensions
    {
        public static void DeleteAllFilesAndSubdirectories(this DirectoryInfo directory)
        {
            foreach (System.IO.FileInfo file in directory.GetFiles("*.*", SearchOption.AllDirectories)) file.Delete();
            foreach (System.IO.DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
        }

    }



}





