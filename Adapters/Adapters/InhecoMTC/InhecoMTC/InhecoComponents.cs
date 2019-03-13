using ComponentInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InhecoMTCdll;
using AdapterBaseClasses;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Threading;
using System.Diagnostics;

namespace InhecoMTC
{

    public enum ShakingShape
    {
        none = -1,
        Circle_Counter_Clockwise = 0,
        Circle_Clockwise = 1,
        Up_Left_Down_Right = 2,
        Up_Right_Down_Left = 3,
        Up_Down = 4,
        Left_Right = 5

    }

    public class InhecoComponent : IComponentAdapter
    {
        public InhecoComponent(string componentName)
        {
            ComponentName = componentName;
            inhecoMtcApi = new GlobCom();
            commandLibrary = new InhecoCommandLibrary();
        }

        public InhecoComponent()
        {
            ComponentName = "Inheco Component";
            inhecoMtcApi = new GlobCom();
            commandLibrary = new InhecoCommandLibrary();
        }


        #region Dependencies

        protected readonly GlobCom inhecoMtcApi;

        protected readonly InhecoCommandLibrary commandLibrary;

        //TODO: POPULATE ERROR DICTIONARY
        private readonly Dictionary<string, string> ErrorLibrary = new Dictionary<string, string>()
        {
            { "1", "External message protocol violation, such as incorrect CRC."},
            { "2", "Internal message protocol violation, such as incorrect CRC."},
            { "3", "Command not executable. Condition for command not fulfilled."},
            { "4", "Command unknown. The command does not exist."},
            { "5", "Wrong parameter."},
            { "6", "Reset detected."},
            { "7", "Slot ID unknown. Slot ID > 6 or slot module plug is empty."},
            { "8", "Wrong keyword. The serial-number-specific keyword was wrong."},
            { "9", "Timeout from slot module. Reset MTC/STC to resolve."},
            { "A", "Device is busy with an action."},
            { "B", "Reserved."},
            { "C", "Housing temperature out of range."},
            { "D", "Communication response time too long."},
            { "E", "Voltage power supply out of range."},
            { "F", "Housing fan blocked or disconnected."},
            { "G", "Device temperature too high."},
            { "H", "RPM too high."},
            { "I", "CPAC voltage out of range."},
            { "K", "TEC current too low."},
            { "R", "Cable break or short with thermocouple."},
            { "T", "Temperature difference too high between main sensor and supervisor sensor."},
            { "W", "Wrong device connected."},
        };

        #endregion

        #region COMPONENT_PROPS

        [Display]
        [DataMember]
        public DEVICE_ID DeviceId { get; set; }

        [Display]
        [DataMember]
        public SLOT_ID SlotId { get; set; }

        [Display]
        [DataMember]
        public int ActionTimeout { get; set; }


        #endregion

        #region COMPONENT_STATE

        [Display]
        [ComponentState(memberName: "Last Reported Device Type", memberAlias: "Last Reported Device Type", memberDescription: "The inheco MTC slot device type", memberId: "_deviceType")]
        public DEVICE_TYPE DeviceType { get; set; }

        [Display]
        [ComponentState(memberName: "Last Reported Firmware Version", memberAlias: "Last Reported Firmware Version", memberDescription: "Servey of all firmware running on inheco MTC", memberId: "_firmwareVersion")]
        public string FirmwareVersion { get; set; }

        [Display]
        [ComponentState(memberName: "Last Reported Heating Status", memberAlias: "Last Reported Heating Status", memberDescription: "The current heating status", memberId: "_heatingStatus")]
        public HEATING_STATUS HeatingStatus { get; set; }

        [Display]
        [ComponentState(memberName: "Last Reported Temperature Set Point", memberAlias: "Last Reported Temperature Set Point", memberDescription: "The current temperature set point", memberId: "_heatingSetPoint")]
        public double TemperatureSetPoint { get; set; }

        [Display]
        [ComponentState(memberName: "Last Reported Temperature Units", memberAlias: "Last Reported Temperature Units", memberDescription: "The temperature units used when setting or reporting temperature set point", memberId: "_temperatureUnits")]
        public string TemperatureUnits { get; set; }

        [Display]
        [ComponentState(memberName: "Last Reported Max Allowed Temperature", memberAlias: "Last Reported Max Allowed Temperature", memberDescription: "The max allowed temperature set point value", memberId: "_maxAllowedTemp")]
        public double MaxAllowedTemp { get; set; }

        [Display]
        [ComponentState(memberName: "Last Reported Min Allowed Temperature", memberAlias: "Last Reported Min Allowed Temperature", memberDescription: "The min allowed temperature set point value", memberId: "_minAllowedTemp")]
        public double MinAllowedTemp { get; set; }


        #endregion

        #region ENCAPSULATION_METHODS

        protected string ExecuteActionWithTimeout(Func<string> action, int millisecondsTimeout)
        {
            #region preconditions

            string failPrefix = "Failed to execute action. ";
            if (action == null)
                throw new ArgumentNullException(nameof(action), failPrefix + "The action to execute cannot be null");
            if (millisecondsTimeout < 1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), failPrefix + "The time cannot be less than 1 millisecond");

            #endregion

            Task<string> actionTask = Task.Run(action);
            bool succeeded = actionTask.Wait(millisecondsTimeout: millisecondsTimeout);

            if (!succeeded)
                throw new TimeoutException(failPrefix + "Action timed out. Allotted time: " + millisecondsTimeout);

            return actionTask.Result;

        }

        protected string ExecuteReadWriteToInhecoActionWithTimeout(string commandText)
        {
            if (string.IsNullOrEmpty(commandText))
                throw new ArgumentNullException(nameof(commandText), "Failed to execute action. Command text cannot be null");

            string resp = ExecuteActionWithTimeout(action: () =>
            {
                inhecoMtcApi.WriteOnly(commandText);//write command
                return inhecoMtcApi.ReadSync();//read response
            },
            millisecondsTimeout: ActionTimeout);

            return resp;
        }

        protected string ExecuteActionOnInhecoActionWithTimeout(string commandText, string failureText)
        {
            if (string.IsNullOrEmpty(commandText))
                throw new ArgumentNullException(nameof(commandText), failureText + "Command text cannot be null");

            string resp = ExecuteReadWriteToInhecoActionWithTimeout(commandText: commandText);

            ThrowIfErrorResponse(failPrefix: failureText, resp: resp, initialCommandText: commandText);//throw if response contains error code

            return resp;
        }

        protected bool IsResponseError(string cleanRespMessage)
        {
            #region preconditions
            string failPrefix = "Failed to check if response had an error code. ";
            if (string.IsNullOrEmpty(cleanRespMessage))
                throw new ArgumentNullException(nameof(cleanRespMessage), failPrefix + "Response message cannot be null");

            #endregion

            string errorCode = cleanRespMessage.Substring(startIndex: 0, length: 1);

            bool isError = true;

            if (errorCode == "0")
                isError = false;

            return isError;

        }

        protected void ThrowOnEchoNotPresent(string failPrefix, string response, string initialCommandText)
        {
            #region preconditions

            if (string.IsNullOrEmpty(response))
                throw new ArgumentNullException(nameof(response), failPrefix + "Response text cannot be empty");
            if (response.Length < 4)
                throw new ArgumentException(nameof(response), failPrefix + "Response text cannot be less than 4 characters in length");
            if (string.IsNullOrEmpty(initialCommandText))
                throw new ArgumentNullException(nameof(initialCommandText), failPrefix + "Initial command text cannot be empty");

            #endregion

            string expectedEcho = initialCommandText.Substring(startIndex: 0, length: 4).ToLower();
            string echoFragment = response.Substring(startIndex: 0, length: 4);

            bool areEqual = expectedEcho.Equals(echoFragment, StringComparison.InvariantCulture);

            if (!areEqual)
                throw new InhecoMtcException(failPrefix + "The actual echo text: " + echoFragment + " did not match the expected echo text: " + expectedEcho);
        }

        protected string RemoveEcho(string response, string initialCommandText)
        {

            #region preconditions

            string failPrefix = "Failed to remove echo text from response text. ";

            if (string.IsNullOrEmpty(response))
                throw new ArgumentNullException(nameof(response), failPrefix + "Response text cannot be empty");
            if (string.IsNullOrEmpty(initialCommandText))
                throw new ArgumentNullException(nameof(initialCommandText), failPrefix + "Initial command text cannot be empty");

            #endregion


            //remove echo (first four chars of command, with inverted case) from response, then remove return code
            string cleanResponse = response.Replace(initialCommandText.Substring(startIndex: 0, length: 4).ToLower(), string.Empty);

            if (string.IsNullOrEmpty(cleanResponse))
                throw new NullReferenceException(failPrefix + "Response was empty after removing echoed text");

            return cleanResponse;
        }

        protected string RemoveEchoAndErrorCode(string response, string initialCommandText)
        {

            #region preconditions

            string failPrefix = "Failed to remove echo text from response text. ";

            if (string.IsNullOrEmpty(response))
                throw new ArgumentNullException(nameof(response), failPrefix + "Response text cannot be empty");
            if (string.IsNullOrEmpty(initialCommandText))
                throw new ArgumentNullException(nameof(initialCommandText), failPrefix + "Initial command text cannot be empty");

            #endregion

            //remove echo from response, then remove return code
            string cleanResponse = RemoveEcho(response: response, initialCommandText: initialCommandText).Remove(startIndex: 0, count: 1);

            if (string.IsNullOrEmpty(cleanResponse))
                throw new NullReferenceException(failPrefix + "Response was empty after removing echoed text");

            return cleanResponse;
        }

        protected int GetResponseInt(string response, string initCommandText)
        {
            string cleanResp = RemoveEchoAndErrorCode(response: response, initialCommandText: initCommandText);
            int respInt;
            bool parsed = int.TryParse(cleanResp, out respInt);

            string failPrefix = "Failed to get integer from response. ";

            if (!parsed)
                throw new InvalidCastException(failPrefix + "Could not cast: " + cleanResp + " to an integer.");

            return respInt;
        }

        protected string ConstructSlotCommandText(SLOT_ID slotId, string command, List<string> args = null)
        {
            string failPrefix = "Failed to construct command text. ";

            #region preconditions

            if (slotId == SLOT_ID.None)
                throw new ArgumentNullException(nameof(slotId), failPrefix + "Slot id cannot be none");
            if (string.IsNullOrEmpty(command))
                throw new ArgumentNullException(nameof(command), failPrefix + "Command text cannot be null");

            #endregion

            string commandText = ((int)slotId).ToString() + command;

            if (args != null && args.Count > 0)
            {
                commandText += string.Join(",", args.ToArray());
            }

            return commandText;
        }

        protected string ConstructSystemCommandText(string command, List<string> args = null)
        {
            string failPrefix = "Failed to construct command text. ";

            #region preconditions

            if (string.IsNullOrEmpty(command))
                throw new ArgumentNullException(nameof(command), failPrefix + "Command text cannot be null");

            #endregion

            string commandText = (0).ToString() + command;

            if (args != null && args.Count > 0)
            {
                commandText += string.Join(",", args.ToArray());
            }

            return commandText;
        }

        protected string GetErrorMessageByResponse(string responseNoEcho)
        {
            #region preconditions
            string failPrefix = "Failed to get error by response. ";
            if (string.IsNullOrEmpty(responseNoEcho))
                throw new ArgumentNullException(nameof(responseNoEcho), failPrefix + "Specified response cannot be empty");

            #endregion

            string errorText = responseNoEcho.Substring(startIndex: 0, length: 1);
            string errorMessage = GetErrorMessageFromLibrary(errorCode: errorText);
            return errorMessage;

        }

        protected string GetErrorMessageFromLibrary(string errorCode)
        {
            #region preconditions

            string failPrefix = "Failed to get error message from library. ";

            if (string.IsNullOrEmpty(errorCode))
                throw new ArgumentNullException(nameof(errorCode), failPrefix + "Error code cannot be empty.");

            #endregion

            string errorMessage = string.Empty;
            bool errorFound = ErrorLibrary.TryGetValue(errorCode, out errorMessage);

            if (!errorFound)
                errorMessage = "No error found with code: " + errorCode;

            return errorMessage;

        }

        protected void ThrowIfErrorResponse(string failPrefix, string resp, string initialCommandText)
        {
            if (string.IsNullOrEmpty(resp))
                throw new InhecoMtcException(failPrefix + "No response recieved from device.");

            ThrowOnEchoNotPresent(failPrefix: failPrefix, response: resp, initialCommandText: initialCommandText);
            string respNoEcho = RemoveEcho(response: resp, initialCommandText: initialCommandText);
            bool isError = IsResponseError(cleanRespMessage: respNoEcho);

            if (isError)
                throw new InhecoMtcException(failPrefix + GetErrorMessageByResponse(responseNoEcho: respNoEcho));
        }

        #endregion

        #region PUBLIC_METHODS

        public double ReportMaxAllowedTemp()
        {
            string failPrefix = "Failed to report max allowed temperature. ";
            int conversionFactor = 10;

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ReportMaxAllowedTemp,
                args: new List<string>() { "1" });

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);

            int maxTemp = GetResponseInt(response: resp, initCommandText: command);
            double maxTempConverted = maxTemp / conversionFactor;

            return maxTempConverted;
        }

        public double ReportMinAllowedTemp()
        {
            string failPrefix = "Failed to report minimum allowed temperature. ";
            int conversionFactor = 10;

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ReportMinimumAllowedTemp);

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);
            int minTemp = GetResponseInt(response: resp, initCommandText: command);
            double minTempConverted = minTemp / conversionFactor;

            return minTempConverted;
        }


        [ComponentAction(memberAlias: "Report Firmware Version", memberDescription: "Report the firmware version currently running on device", memberId: "_getFirmwareVersion", isIndependent: false)]
        public string ReportFirmwareVersion()
        {
            string failPrefix = "Failed to get firmware version. ";
            string resp = string.Empty;
            //create collection of firmware types for args
            var firmwareTypes = new Dictionary<string, string>()
            {
                { "0", "Bootstrap Version" },
                { "1", "Application Version" },
                { "2", "Serial Number" },
                { "3", "Current Hardware Version" },
                { "4", "Inheco copyright" },
            };

            //iterate over firmware types
            foreach (var item in firmwareTypes)
            {
                //prepare command
                string command =
                ConstructSlotCommandText(
                    slotId: SlotId,
                    command: commandLibrary.ReportFirmwareVersion,
                    args: new List<string>() { item.Key });

                string tempResp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);
                resp += item.Value + ": " + tempResp + Environment.NewLine;//add to collection of firmware versions
            }

            return resp;

        }


        [ComponentAction(memberAlias: "Report Device Type", memberDescription: "Report the device type", memberId: "_getDeviceType", isIndependent: false)]
        public DEVICE_TYPE ReportDeviceType()
        {
            string failPrefix = "Failed to get device type. ";

            //prepare command
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ReportDeviceType);

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);
            int respInt = GetResponseInt(response: resp, initCommandText: command);//get integer from response message portion
            DEVICE_TYPE devType;//declare device type var
            bool parsed = Enum.TryParse(respInt.ToString(), out devType);//try to parse response to a device type

            //throw if fails to parse
            if (!parsed)
                throw new InvalidCastException(failPrefix + "Failed to convert return message: " + respInt.ToString() + " to device type");

            //return device type
            return devType;

        }

        public void SetTargetTemp(double targetTemp)
        {
            #region preconditions

            string failPrefix = "Failed to set target temp. ";
            double max = ReportMaxAllowedTemp();
            double min = ReportMinAllowedTemp();

            if (targetTemp > max)
                throw new ArgumentOutOfRangeException(nameof(targetTemp), failPrefix + "Specified temperature set point: " + targetTemp + " was greater than max allowed temp: " + max);
            if (targetTemp < min)
                throw new ArgumentOutOfRangeException(nameof(targetTemp), failPrefix + "Specified temperature set point: " + targetTemp + " was less than min allowed temp: " + min);

            #endregion

            int conversionFactor = 10;

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.SetTargetTemp,
                args: new List<string>() { (targetTemp * conversionFactor).ToString() });

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);

        }

        [ComponentAction(memberAlias: "Report Target Temperature", memberDescription: "Reports the target temperature", memberId: "_reportTargetTemp", isIndependent: false)]
        public double ReportTargetTemp()
        {
            string failPrefix = "Failed to report target temp. ";

            int conversionFactor = 10;

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ReportTargetTemp);

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);
            int temp = GetResponseInt(response: resp, initCommandText: command);//get int representation of temp in degrees C
            double convertedTemp = temp / conversionFactor;//reported value is in 1/10 C, so convert it

            return convertedTemp;

        }


        [ComponentAction(memberAlias: "Report Heater Enabled Status", memberDescription: "Reports the heater enabled status", memberId: "_reportHeaterEnabledStatus", isIndependent: false)]
        public HEATING_STATUS ReportHeaterEnabledStatus()
        {
            string failPrefix = "Failed to report heater enabled status. ";

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ReportHeaterEnabledStatus);

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);
            int isEnabled = GetResponseInt(response: resp, initCommandText: command);//get int representation of temp in degrees C

            HEATING_STATUS heatingStatus;

            bool parsed = Enum.TryParse(isEnabled.ToString(), out heatingStatus);

            if (!parsed)
                throw new InvalidCastException(failPrefix + "Failed to convert: " + isEnabled.ToString() + " to a heating status");

            return heatingStatus;

        }

        [ComponentAction(memberAlias: "Enable Heater", memberDescription: "Turns the heater on and attempts to heat to the specified temperature", memberId: "_enableHeater", isIndependent: false)]
        public void EnableHeater(
            [ComponentActionParameter(memberAlias:"Temperature Set Point", memberDescription: "The temperature set point in degrees celcius", memberId: "_tempSetPoint")]
            double temp)
        {
            SetTargetTemp(targetTemp: temp);
            EnableHeater();
        }

        public void EnableHeater()
        {
            string failPrefix = "Failed to enable heater. ";

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ToggleHeatingEnabled,
                args: new List<string>() { "1" });

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);

        }

        [ComponentAction(memberAlias: "Disable Heater", memberDescription: "Turns the heater off", memberId: "_disableHeater", isIndependent: false)]
        public void DisableHeater()
        {
            string failPrefix = "Failed to disable heater. ";

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ToggleHeatingEnabled,
                args: new List<string>() { "0" });

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);

        }


        #endregion


        #region IComponentAdapter_Imp

        public string ComponentName { get; set; }

        public virtual void CommitConfiguredState() { }

        public void Connect()
        {
            inhecoMtcApi.initDll();
            int found = inhecoMtcApi.FindTheUniversalControl(ID: (int)DeviceId);

            if (found == 0)
                throw new InhecoMtcException("Failed to connect. Device not found with id: " + DeviceId);
        }

        public virtual void Disconnect() { }

        public virtual void Dispose() { }
        public virtual string GetError() { return string.Empty; }
        public virtual void Initialize() { }
        public virtual void InjectServiceProvider(IServiceProvider servProv) { }

        public virtual bool IsConnected()
        {
            return false;
            //TODO: write logic for this
        }

        public virtual void Pause()
        {
            //throw new NotImplementedException();
        }

        public virtual void ReadState()
        {
            HeatingStatus = ReportHeaterEnabledStatus();
            TemperatureUnits = "Celsius";
            TemperatureSetPoint = ReportTargetTemp();
            DeviceType = ReportDeviceType();
            FirmwareVersion = ReportFirmwareVersion();
            MinAllowedTemp = ReportMinAllowedTemp();
            MaxAllowedTemp = ReportMaxAllowedTemp();
        }

        [ComponentAction(memberAlias: "Reset Device", memberDescription: "Resets main MTC controller and slot devices. Note: this takes approximately 30 seconds", memberId: "_resetDevice", isIndependent: true)]
        public virtual void Reset()
        {
            //since independent action, need to call connect and others
            Connect();

            string failPrefix = "Failed to reset device. ";

            //prepare command text
            string command =
            ConstructSystemCommandText(
                command: commandLibrary.ResetSystem,
                args: new List<string>() { "0" });

            inhecoMtcApi.WriteOnly(command);//write command
            Thread.Sleep(22000);

        }

        public virtual void Resume()
        {
            //throw new NotImplementedException();
        }

        public virtual void ShutDown()
        {
            //throw new NotImplementedException();
        }

        public virtual void Stop()
        {
            //throw new NotImplementedException();
        }

        #endregion

    }

    public class Thermoshake : InhecoComponent
    {
        public Thermoshake()
            : base(componentName: "Thermoshake")
        {

        }

        #region COMPONENT_STATE

        [Display]
        [ComponentState(memberName: "Last Reported Shaking Status", memberAlias: "Last Reported Shaking Status", memberDescription: "The current shaking status", memberId: "_shakingStatus")]
        public SHAKING_STATUS ShakingStatus { get; set; }

        [Display]
        [ComponentState(memberName: "Last Reported Shaking Frequency", memberAlias: "Last Reported Shaking Frequency", memberDescription: "The current shaking frequency set point in revolutions per minute", memberId: "_shakingRpm")]
        public int ShakingFrequency { get; set; }

        #endregion


        #region PUBLIC_ACTIONS

        [ComponentAction(memberAlias: "Report Shaking Frequency", memberDescription: "Reports the current shaking frequency in revolution per minute", memberId: "_reportShakingFrequency", isIndependent: false)]
        public int ReportShakingFrequency()
        {

            string failPrefix = "Failed to report shaking frequency. ";

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ReportShakerfrequency);

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);

            int frequency = GetResponseInt(response: resp, initCommandText: command);//get int representation of temp in degrees C

            return frequency;
        }


        [ComponentAction(memberAlias: "Report Shaking Status", memberDescription: "Reports the current shaking status", memberId: "_reportShakingStatus", isIndependent: false)]
        public SHAKING_STATUS ReportShakingStatus()
        {

            string failPrefix = "Failed to report shaking status";

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ReportShakerEnabledStatus);

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);

            int isEnabled = GetResponseInt(response: resp, initCommandText: command);//get int representation of temp in degrees C

            SHAKING_STATUS shakingStatus;

            bool parsed = Enum.TryParse(isEnabled.ToString(), out shakingStatus);

            if (!parsed)
                throw new InvalidCastException(failPrefix + "Failed to convert: " + isEnabled.ToString() + " to a shaking status");

            return shakingStatus;

        }

        [ComponentAction(memberAlias: "Enable Shaking", memberDescription: "Enable shaking at specified rpm", memberId: "_enableShaking", isIndependent: false)]
        public void EnableShaking(
           [ComponentActionParameter(memberAlias:"Rpm", memberDescription:"The shaking frequency set point in revolutions per minute", memberId: "_rpm")]
            int rpm)
        {
            SetShakingFrequency(rpm);
            EnableShaking();
        }


        [ComponentAction(memberAlias: "Enable Shaking Counter Clockwise", memberDescription: "Enable shaking counter clockwise at specified rpm", memberId: "_enableShakingCcw", isIndependent: false)]
        public void EnableShakingCCW(
       [ComponentActionParameter(memberAlias:"Rpm", memberDescription:"The shaking frequency set point in revolutions per minute", memberId: "_rpm")]
            int rpm)
        {
            EnableShaking(rpm: rpm, shakingShape: ShakingShape.Circle_Counter_Clockwise);
        }

        [ComponentAction(memberAlias: "Enable Shaking Clockwise", memberDescription: "Enable shaking clockwise at specified rpm", memberId: "_enableShakingCw", isIndependent: false)]
        public void EnableShakingCW(
           [ComponentActionParameter(memberAlias:"Rpm", memberDescription:"The shaking frequency set point in revolutions per minute", memberId: "_rpm")]
            int rpm)
        {
            EnableShaking(rpm: rpm, shakingShape: ShakingShape.Circle_Clockwise);
        }

        protected void EnableShaking(int rpm, ShakingShape shakingShape)
        {
            //set shape
            SetShakingShape(shape: shakingShape);
            //start shaking
            EnableShaking(rpm: rpm);
        }



        protected void SetShakingShape(ShakingShape shape)
        {
            #region preconditions

            string failPrefix = "Failed to set shaking frequency. ";
            if (shape == ShakingShape.none)
                throw new ArgumentOutOfRangeException(nameof(shape), failPrefix + "Shaking direction cannot be none");

            #endregion

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.SetShakerShape,
                args: new List<string>() { ((int)shape).ToString() });
            //args: new List<string>() { ((int)shape).ToString(), "1" });

            inhecoMtcApi.WriteOnly(command);//write command
            string resp = inhecoMtcApi.ReadSync();//read response
            ThrowIfErrorResponse(failPrefix: failPrefix, resp: resp, initialCommandText: command);//throw if response contains error code

        }


        public void SetShakingFrequency(int rpm)
        {
            #region preconditions

            string failPrefix = "Failed to set shaking frequency. ";
            if (rpm < 0)
                throw new ArgumentOutOfRangeException(nameof(rpm), failPrefix + "Rpm cannot be negative.");

            #endregion

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.SetShakerfrequency,
                args: new List<string>() { rpm.ToString() });

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);

        }

        public void EnableShaking()
        {
            string failPrefix = "Failed to enable shaking. ";

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ToggleShakingEnabled,
                args: new List<string>() { "1" });

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);

        }

        [ComponentAction(memberAlias: "Disable Shaking", memberDescription: "Disables shaking", memberId: "_disableShaking", isIndependent: false)]
        public void DisableShaking()
        {
            string failPrefix = "Failed to disable shaking. ";

            //prepare command text
            string command =
            ConstructSlotCommandText(
                slotId: SlotId,
                command: commandLibrary.ToggleShakingEnabled,
                args: new List<string>() { "0" });

            string resp = ExecuteActionOnInhecoActionWithTimeout(commandText: command, failureText: failPrefix);

        }



        #endregion

        #region IComponentAdapter_Overrides


        public override void ReadState()
        {
            base.ReadState();

            ShakingStatus = ReportShakingStatus();
            ShakingFrequency = ReportShakingFrequency();

        }

        #endregion


    }

    public class InhecoCoolingBlock : InhecoComponent
    {
        public InhecoCoolingBlock()
            : base(componentName: "Inheco Cooling Block")
        {

        }




    }

    public enum DEVICE_ID
    {
        None = -1,
        One,
        Two,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight
    }

    public enum SLOT_ID
    {
        None = 0,
        One,
        Two,
        Three,
        Four,
        Five,
        Six
    }

    public enum DEVICE_TYPE
    {
        Thermoshake = 0,
        CPAC = 1,
        Teleshake = 2,
        CPAC_2_TEC = 4,
        Undefined = 255
    }

    public enum HEATING_STATUS
    {
        Heating = 0,
        Cooling,
        Off
    }

    public enum SHAKING_STATUS
    {
        Off = 0,
        On = 1
    }

    public class InhecoMtcException : Exception
    {
        public InhecoMtcException(string message) :
            base(message: message)
        {

        }
    }

    public class InhecoCommandLibrary
    {
        public string ReportError = "REC";
        public string ReportFirmwareVersion = "RFV";
        public string ReportDeviceType = "RTD";
        public string SetTargetTemp = "STT";
        public string ReportTargetTemp = "RTT";
        public string ReportHeaterEnabledStatus = "RHE";
        public string SetRoomTemp = "SRT";
        public string ReportShakerfrequency = "RSR";
        public string SetShakerfrequency = "SSR";
        public string ReportShakerEnabledStatus = "RSE";
        public string ToggleShakingEnabled = "ASE";
        public string ToggleHeatingEnabled = "ATE";
        public string SetShakingTime = "SST";
        public string ResetSystem = "SRS";
        public string ReportMinimumAllowedTemp = "RLT";
        public string ReportMaxAllowedTemp = "RMT";
        public string ReportDiagnosticsCounter = "RDC";
        public string SetShakerShape = "SSS";

    }

}
