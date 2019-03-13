using AdapterBaseClasses;
using ComponentInterfaces;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;

namespace LunaticAdapter
{
    public class LunaticComponent : IComponentAdapter
    {
        public LunaticComponent()
        {
            ComponentName = "Lunatic";
            fileUtility = new FileUtility();
        }


        #region ClassDependencies


        private ILunaticAPI lunaticAPI;
        private readonly IFileUtility fileUtility;

        #endregion

        #region Properties

        [DataMember]
        [Display]
        public string IpAddress { get; set; }

        [DataMember]
        [Display]
        public int Port { get; set; }


        #endregion

        #region Encapsulation_Methods
        // All helper Methods Private or Protected. Internal Code to class.

        /// <summary>
        /// Reference Lazy loading and inversion of control design patterns.
        /// Mechanism for getting an instance of the lunatic API object while abstracting concrete type
        /// This provides "just in time" access to the dependency resource while retaining efficiency by implementing lazy-loading-enabled instantiation
        /// </summary>
        protected ILunaticAPI GetLunaticAPI()
        {
            string error = "Failed to get Lunatic API instance. ";

            if (lunaticAPI == null)
            {
                IPAddress tempAddress;
                if (string.IsNullOrEmpty(IpAddress))
                    throw new ArgumentNullException(nameof(IpAddress), error + "IpAddress cannot be empty");
                if (Port == default(int))
                    throw new ArgumentNullException(nameof(Port), error + "Port cannot be: " + default(int));
                if (!IPAddress.TryParse(ipString: IpAddress, address: out tempAddress))
                    throw new FormatException(error + "This IPAddress: " + IpAddress + "is not of expected format:");
                lunaticAPI = new LunaticAPI(IPAddress: IpAddress, port: Port);
            }

            return lunaticAPI;

        }
        #endregion


        [Display]
        [ComponentState(memberName: "Device Status", memberAlias: "Device Status", memberDescription: "The current state of the device", memberId: "_currentStatus")]
        public string CurrentStatus { get; set; }



        [ComponentAction(memberAlias: "Get Device Reading Status", memberDescription: "Checks if device is currently reading a plate", memberId: "_isReading", isIndependent: false)]
        public bool IsReading()
        {
            int actionSuccessful = 0;
            int measuringCode = 31;
            string errorPrefix = "Failed to check reading status. ";
            var lunatic = GetLunaticAPI();
            string measurementInfo;
            int retval = lunatic.GetStatus(out measurementInfo);

            if (retval != actionSuccessful)
                throw new LunaticException(errorPrefix + lunatic.GetLunaticError(retval));

            bool isMeasuring = false;

            if (retval == measuringCode)
                isMeasuring = true;

            return isMeasuring;
        }

        [ComponentAction(memberAlias: "Open Tray", memberDescription: "Opens the plate tray", memberId: "_openTray", isIndependent: false)]
        public void OpenTray()
        {
            int actionSuccessful = 0;
            int trayOpenErrorCode = 3;
            string errorPrefix = "Failed to open device tray. ";
            var lunatic = GetLunaticAPI();
            int retval = lunatic.OpenTray();
            if (retval != actionSuccessful)
            {
                if (retval != trayOpenErrorCode)//dont throw if already open 
                    throw new LunaticException(errorPrefix + lunatic.GetLunaticError(retval));
            }

        }

        [ComponentAction(memberAlias: "Close tray", memberDescription: "Closes the plate tray", memberId: "_closeTray", isIndependent: false)]
        public void CloseTray()
        {
            int actionSuccessful = 0;
            int trayClosedErrorCode = 4;
            string errorPrefix = "Failed to close device tray. ";
            var lunatic = GetLunaticAPI();
            int retval = lunatic.CloseTray();

            if (retval != actionSuccessful)
            {
                if (retval != trayClosedErrorCode)
                    throw new LunaticException(errorPrefix + lunatic.GetLunaticError(retval));
            }
        }

        [ComponentAction(memberAlias: "Read plate", memberDescription: "Reads the plate", memberId: "_readPlate", isIndependent: false)]
        public void ReadPlate(
            [ComponentActionParameter(memberAlias: "Experiment Definition File Path", memberDescription: "File path for experiment definition rules", memberId: "_expDefinitionFilePath")]
            string expDefinitionFilePath,
            [ComponentActionParameter(memberAlias: "Samples File Path", memberDescription: "File path of samples", memberId: "_samplesFilePath")]
            string samplesFilePath,
            [ComponentActionParameter(memberAlias: "Results Headers File Path", memberDescription: "File path of results headers", memberId: "_resultsHeadersFilePath")]
            string resultsHeadersFilePath
            )
        {
            string errorMessage = "Failed to read plate. ";

            #region Validation

            if (!File.Exists(expDefinitionFilePath))
                throw new FileNotFoundException(errorMessage + " File note found at: " + expDefinitionFilePath);
            if (!File.Exists(samplesFilePath))
                throw new FileNotFoundException(errorMessage + "File not found at: " + samplesFilePath);
            if (!File.Exists(resultsHeadersFilePath))
                throw new FileNotFoundException(errorMessage + "File not found at: " + resultsHeadersFilePath);

            #endregion

            // Get Lunatic API
            var lunaticAPI = GetLunaticAPI();

            string expDefContent = fileUtility.GetContents(expDefinitionFilePath);
            string samplesContent = fileUtility.GetContents(samplesFilePath);
            string resultsHeaders = fileUtility.GetContents(resultsHeadersFilePath);

            string[] plates;
            int actionSuccessful = 0;
            int retVal = lunaticAPI.DefineExperiment(expDefContent, samplesContent, out plates);
            if (retVal != actionSuccessful)
                throw new LunaticException(errorMessage + lunaticAPI.GetLunaticError(retVal));

            if (plates.Count() != 1)
                throw new NotSupportedException(errorMessage + "File contains more than one plate.");

            retVal = lunaticAPI.Measure(plates.First());
            if (retVal != actionSuccessful)
                throw new LunaticException(errorMessage + lunaticAPI.GetLunaticError(retVal));

        }


        #region IComponentAdapter_Implementation

        public string ComponentName { get; set; }

        public void CommitConfiguredState()
        {
            return;
        }

        public void Connect()
        {
            int actionSuccessful = 0;
            string errorPrefix = "Failed to connect to Lunatic. ";
            var lunatic = GetLunaticAPI();
            int retval = lunatic.RequestAccess();
            if (retval != actionSuccessful)
                throw new LunaticException(errorPrefix + lunatic.GetLunaticError(retval));
        }

        public void Disconnect()
        {
            int actionSuccessful = 0;
            string errorPrefix = "Failed to disconnect from Lunatic. ";
            var lunatic = GetLunaticAPI();
            int retval = lunatic.ReleaseAccess();
            if (retval != actionSuccessful)
                throw new LunaticException(errorPrefix + lunatic.GetLunaticError(retval));
        }

        public void Dispose()
        {
            Disconnect();
        }

        public string GetError()
        {
            string errorPrefix = "Lunatic status/error: ";
            var lunatic = GetLunaticAPI();
            int retval = lunatic.GetStatus(out string measurementInfo);
            return errorPrefix + lunatic.GetLunaticError(retval);
        }

        public void Initialize()
        {
            //no logic
        }

        public void InjectServiceProvider(IServiceProvider servProv)
        {
            //no logic
        }

        public bool IsConnected()
        {
            bool isConnected = false;

            var lunatic = GetLunaticAPI();
            try
            {
                int couldNotConnected = -200;
                int retval = lunatic.GetStatus(out string mesInfo);
                if (retval != couldNotConnected)
                    isConnected = true;
            }
            catch (Exception)
            {
                isConnected = false;
            }
            return isConnected;
        }

        public void Pause()
        {
            //no logic
        }

        public void ReadState()
        {
            string status = GetError();
            CurrentStatus = status;
        }

        public void Reset()
        {
            //no logic
        }

        public void Resume()
        {
            //no logic
        }

        public void ShutDown()
        {
            //no logic
        }

        public void Stop()
        {
            string errorPrefix = "Failed to abort Lunatic measurement. ";
            var lunatic = GetLunaticAPI();
            int retval = lunatic.Abort_Measurement();
            if (retval != 0)
                throw new LunaticException(errorPrefix + lunatic.GetLunaticError(retval));
        }

        #endregion
    }


    //Custom Exception for Error Library
    public class LunaticException : Exception
    {
        public LunaticException(string message) : base(message: message)
        {

        }
    }
}
