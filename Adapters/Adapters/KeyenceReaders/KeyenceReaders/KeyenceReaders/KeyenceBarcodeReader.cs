using AdapterBaseClasses;
using CommonServiceInterfaces;
using ComponentInterfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace KeyenceBarcodeReaders
{


    [DataContract]
    public class KeyenceBarcodeReader : TCP_IP_Adapter, ISensorAdapter
    {

        //for serialization 
        protected KeyenceBarcodeReader() : base()
        {
            InitializeMembers();
        }

        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {

            MessageEncoding = Encoding.ASCII;
            TerminationCharacters = new string[] { KeyenceSpecialCharacters.TerminationCharacter };
            
        }

        public KeyenceBarcodeReader(
            ICodeContractService codeContractDependency,
            ITypeManipulator typeManipDependency)
            : base(codeContractDependency)
        {

            if (_typeManipulatorDependency == null)
            {
                _typeManipulatorDependency = typeManipDependency;
            }
            else
            {
                throw new ApplicationException("");
            }

            InitializeMembers();

           
        }


        private void InitializeMembers()
        {
            MessageEncoding = Encoding.ASCII;
            TerminationCharacters = new string[] { KeyenceSpecialCharacters.TerminationCharacter };
            Port = 9004;
            ComponentName = "Keyence Barcode Reader";
            ReadTimeout = 10000;//10s
            WriteTimeout = 10000;//10s
            RemoteActionTimeout = 20000;
        }

        public ITypeManipulator _typeManipulatorDependency;


        #region Properties


        #endregion

        #region Keyence_MessageDictionaries

        Dictionary<ErrorCodes, string> ErrorCodeDescriptions = new Dictionary<ErrorCodes, string>
        {
            { ErrorCodes.NONE, "No Errors" },
            { ErrorCodes.READING_ERROR, "Reading Error"},
            { ErrorCodes.PRESET_DATA_MISMATCH, "The read code does not match the preset data."},
            { ErrorCodes.NO_CODE_FOUND, "The code could not be found within the field of view while tuning."},
            { ErrorCodes.TUNING_ABORTED, "Tuning was aborted midway."},
            { ErrorCodes.CONCURRENT_OPERATIONS_ATTEMPTED, "Another operation instruction was received during operation. (Operation instruction is not performed.)"},
            { ErrorCodes.INVALID_BANK_NUMBER, "The bank number specification is invalid (other than 1 to 16)."},
            { ErrorCodes.INVALID_PRESET_DATA, "Preset data specification is invalid. (Specified size is outside the range.)"},
            { ErrorCodes.BUFFER_OVERFLOW, "Shortage of specified size (Result data and present data size is beyond the limit.)"}
        };

        public enum ErrorCodes
        {
            NONE = 0,
            READING_ERROR = 201,
            PRESET_DATA_MISMATCH = 202,
            NO_CODE_FOUND = 210,
            TUNING_ABORTED = 213,
            CONCURRENT_OPERATIONS_ATTEMPTED = 120,
            INVALID_BANK_NUMBER = 102,
            INVALID_PRESET_DATA = 220,
            BUFFER_OVERFLOW = 230
        }

        Dictionary<ErrorStatuses, string> ErrorStatusDescriptions = new Dictionary<ErrorStatuses, string>
        {
            { ErrorStatuses.none, "No error" },
            { ErrorStatuses.system, "System error"},
            { ErrorStatuses.update, "Program update error"},
            { ErrorStatuses.cfg, "Set value error"},
            { ErrorStatuses.ip, "IP address duplication error"},
            { ErrorStatuses.over, "Send buffer overflow"},
            { ErrorStatuses.plc, "PLC link error"},
            { ErrorStatuses.profinet, "Profinet error"},
            { ErrorStatuses.lua, "Script error"}
        };

        public enum ErrorStatuses
        {
            none = 0,
            system = 1,
            update = 2,
            cfg = 3,
            ip = 4,
            over = 5,
            plc = 6,
            profinet = 7,
            lua = 8
        }

        Dictionary<BusyStatuses, string> BusyStatusDescriptions = new Dictionary<BusyStatuses, string>
        {
            { BusyStatuses.none, "Not busy" },
            { BusyStatuses.update, "Program updating"},
            { BusyStatuses.file, "Saving the file"},
            { BusyStatuses.trg, "Trigger busy (Currently scanning)"}
        };

        public enum BusyStatuses
        {
            none = 0,
            update = 1,
            file = 2,
            trg = 3
        }


        public enum LockStatuses
        {
            NONE = 0,
            LOCK = 1,
            UNLOCK = 2
        }

        #endregion

        #region EncapsulationMethods

        private string ConstructWriteCommandText(string commandText)
        {
            string fullCommandText = commandText + KeyenceSpecialCharacters.TerminationCharacter;
            return fullCommandText;

        }

        private bool IsResponseSuccess(string responseText)
        {
            int errorTextLength = 2;

            #region Preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(responseText), "Failed to assess response. Response text cannot be null. Device: " + ComponentName);
            _codeContractDependency.Requires<IndexOutOfRangeException>(responseText.Length >= errorTextLength, "Failed to assess response. Response text not the correct length. Device: " + ComponentName);
            #endregion

            bool isSuccess = !responseText.Substring(0, errorTextLength).Equals(KeyenceSpecialCharacters.ErrorCharacter);

            return isSuccess;
        }

        private string[] SplitResponseIntoComponents(string responseText)
        {

            #region Preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(responseText), "Failed to split response. Response text cannot be null. Device: " + ComponentName);
            #endregion

            responseText = responseText.Replace(KeyenceSpecialCharacters.TerminationCharacter, string.Empty);
            string[] responseComponents = responseText.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);


            #region Postconditions
            _codeContractDependency.Requires<ArgumentOutOfRangeException>(responseComponents.Count() == 3, "Failed to split response. Split response output wrong number of sections. Device: " + ComponentName);
            #endregion

            return responseComponents;
        }

        private bool TryParseErrorFromResponse(string responseText, out string errorMessage)
        {
            #region Preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(responseText), "Failed to parse error from response. Response text cannot be null. Device: " + ComponentName);
            #endregion

            bool isSuccessful = false;

            try
            {
                errorMessage = "Unknown";

                string[] responseComponents = responseText.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                if (responseComponents.Length != 3)
                {
                    errorMessage = "Error response did not contain three segments seperated by commas. Original text: " + responseText;
                }
                else
                {
                    string errorCodeText = responseComponents[2];
                    int errorCode;

                    if (!_typeManipulatorDependency.TryParseNumericFromString(errorCodeText, out errorCode))
                    {
                        errorMessage = "Could not parse error code from error text: " + errorCodeText + ". Orignal error text: " + responseText;
                    }
                    else
                    {

                        if (!Enum.IsDefined(typeof(ErrorCodes), errorCode))
                        {
                            errorMessage = "Error code not found in error code enum. Original error text: " + responseText;
                        }
                        else
                        {
                            try
                            {
                                ErrorCodes errorCodeEnum = (ErrorCodes)errorCode;

                                if (!ErrorCodeDescriptions.TryGetValue(errorCodeEnum, out errorMessage))
                                {
                                    errorMessage = "Could not find error code: " + errorCode.ToString() + " in error dictionary. Original error text: " + responseText;
                                }
                                else
                                {
                                    isSuccessful = true;
                                }
                            }
                            catch (InvalidCastException)
                            {
                                errorMessage = "Could not cast to error code enum. Original error text: " + responseText;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                errorMessage = "Failed to parse error. Exception occurred: " + e.Message;
            }


            return isSuccessful;

        }

        private bool TryParseErrorStatus(string errorStatusText, out string errorMessage)
        {
            #region Preconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(errorStatusText), "Failed to parse error status. Statuse text cannot be empty. Device: " + ComponentName);
            #endregion

            bool isSuccessful = false;

            try
            {
                errorMessage = "Unknown";

                string[] responseComponents = SplitResponseIntoComponents(errorStatusText);

                string errorCodeText = responseComponents[2];

                if (!Enum.IsDefined(typeof(ErrorStatuses), errorCodeText))
                {
                    errorMessage = "Error status not found in error status enum. Original error text: " + errorStatusText;
                }
                else
                {
                    try
                    {
                        ErrorStatuses errorStatusEnum = (ErrorStatuses)Enum.Parse(typeof(ErrorStatuses), errorCodeText);

                        if (!ErrorStatusDescriptions.TryGetValue(errorStatusEnum, out errorMessage))
                        {
                            errorMessage = "Could not find error code: " + errorCodeText + " in error dictionary. Original error text: " + errorStatusText;
                        }
                        else
                        {
                            isSuccessful = true;
                        }
                    }
                    catch (Exception)
                    {
                        errorMessage = "Could not cast to error code enum. Original error text: " + errorStatusText;
                    }
                }

            }
            catch (Exception e)
            {
                errorMessage = "Failed to parse error. Exception occurred: " + e.Message;
            }


            return isSuccessful;

        }

        private bool IsScannerBusy()
        {
            bool isBusy;
            string commandMessage = ConstructWriteCommandText(KeyenceCommandPrefixes.BusyStatus);
            string response = WriteRead(commandMessage);

            if (response.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).Length == 3)
            {
                string[] responseComponents = SplitResponseIntoComponents(response);
                string busyCodeText = responseComponents[2];
                BusyStatuses busyStatusEnum = (BusyStatuses)Enum.Parse(typeof(BusyStatuses), busyCodeText);
                isBusy = !(busyStatusEnum == BusyStatuses.none);
            }
            else
            {
                isBusy = true;
            }

            return isBusy;

        }


        private string WriteRead(string commandMessage)
        {

            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(commandMessage), "Failed to write. Text cannot be empty. Device: " + ComponentName);

            string response = "Unknown";

            Write(commandMessage);
            response = Read();

            #region PostConditions
            _codeContractDependency.Requires<InvalidOperationException>(IsResponseSuccess(response), "Device failed during command execution. Raw error response: " + response + ". Device: " + ComponentName);
            #endregion

            return response;
        }

        public string ParseBarcodeFromResponseText(string responseText)
        {
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(responseText), "Failed to parse barcode from response text. Response text cannot be null. Device: " + ComponentName);

            string barcode = responseText.Trim(new char[] { ' ', '\r', '\n' });

            return barcode;

        }

        #endregion


        #region ISensorAdapter



        [ComponentAction(
            memberAlias: "Scan Barcode",
            memberDescription: "Scan until barcode read",
            memberId: "_scan_barcode",
            isIndependent: false)]
        public string Scan()
        {
            //init raw read bc
            string barcode = string.Empty;
            //init parsed bc
            string parsedBarcode = string.Empty;
            //construct command text
            string commandText = ConstructWriteCommandText(KeyenceCommandPrefixes.BeginScanning);

            //attempt reading
            try
            {

                WriteRead(commandText);//do not wait for remote action because the read will block until timeout or bc read
                ReadTimeout = RemoteActionTimeout;//Nothing will be in the network stream until a bc is read, so set read time to remote action timeout
                barcode = Read();
            }
            finally
            {
                //always stop scanning
                Stop();
            }

            parsedBarcode = ParseBarcodeFromResponseText(barcode);

            //return read barcode
            return parsedBarcode;
        }

        public override string GetError()
        {
            //get error status
            string commandText = ConstructWriteCommandText(KeyenceCommandPrefixes.ErrorStatus);
            string errorText = WriteRead(commandText);
            string errorMessage;

            //try to parse error text
            TryParseErrorStatus(errorText, out errorMessage);

            //return error
            return errorMessage;
        }


        public override void Initialize()
        {

        }

        public override void Pause()
        {
            //pause only if busy
            bool isBusy;
            string commandText = ConstructWriteCommandText(KeyenceCommandPrefixes.BusyStatus);
            string response = WriteRead(commandText);
            string[] responseComponents = SplitResponseIntoComponents(response);
            string busyCodeText = responseComponents[2];
            BusyStatuses busyStatusEnum = (BusyStatuses)Enum.Parse(typeof(BusyStatuses), busyCodeText);
            isBusy = !(busyStatusEnum == BusyStatuses.none);
            if (isBusy)
            {
                string commandStop = ConstructWriteCommandText(KeyenceCommandPrefixes.CancelScanning);
                WriteRead(commandStop);
            }
        }


        [ComponentAction(
            memberAlias: "Stop",
            memberDescription: "Stop Scanning",
            memberId: "_stop",
            isIndependent: false)]
        public override void Stop()
        {
            //stop only if busy
            bool isBusy;
            string commandText = ConstructWriteCommandText(KeyenceCommandPrefixes.BusyStatus);
            string response = WriteRead(commandText);
            string[] responseComponents = SplitResponseIntoComponents(response);
            string busyCodeText = responseComponents[2];
            BusyStatuses busyStatusEnum = (BusyStatuses)Enum.Parse(typeof(BusyStatuses), busyCodeText);
            isBusy = !(busyStatusEnum == BusyStatuses.none);
            if (isBusy)
            {
                string commandStop = ConstructWriteCommandText(KeyenceCommandPrefixes.CancelScanning);
                WriteRead(commandStop);
            }

        }

        [ComponentAction(
            memberAlias: "Reset",
            memberDescription: "Reset reader",
            memberId: "_reset",
            isIndependent: false)]
        public override void Reset()
        {
            string commandText = ConstructWriteCommandText(KeyenceCommandPrefixes.SoftReset);
            WriteRead(commandText);

        }

        public override void Resume()
        {
            //check lock status
            bool isLocked;
            string commandText = ConstructWriteCommandText(KeyenceCommandPrefixes.LockStatus);
            string response = WriteRead(commandText);
            string[] responseComponents = SplitResponseIntoComponents(response);
            string lockCodeText = responseComponents[2];
            LockStatuses lockStatusEnum = (LockStatuses)Enum.Parse(typeof(LockStatuses), lockCodeText);
            isLocked = lockStatusEnum == LockStatuses.LOCK;

            //unlock if locked
            if (isLocked)
            {
                string commandTextUnlock = ConstructWriteCommandText(KeyenceCommandPrefixes.Unlock);
                WriteRead(commandTextUnlock);
            }

            //no explicit resume function
        }

        [ComponentAction(
            memberAlias: "Shutdown",
            memberDescription: "Stop scanning and disconnect",
            memberId: "_shutdown",
            isIndependent: false)]
        public override void ShutDown()
        {
            Stop();
            Reset();
            Disconnect();
        }

        public override void InjectServiceProvider(IServiceProvider servProv)
        {
            base.InjectServiceProvider(servProv);
            _typeManipulatorDependency = (ITypeManipulator)servProv.GetService(typeof(ITypeManipulator));
        }


        public override void CommitConfiguredState()
        {
            return;
        }

        public override void ReadState()
        {
            return;
        }


        #endregion


        #region FixedTextAndEnums

        internal static class KeyenceSpecialCharacters
        {
            internal const string TerminationCharacter = "\r";
            internal const string ErrorCharacter = "ER";
        }

        internal static class KeyenceCommandPrefixes
        {
            internal const string BeginScanning = "LON";
            internal const string StopScanning = "LOFF";
            internal const string CancelScanning = "CANCEL";//Command to force to finish of the running scanning operation (also reading operation).
            internal const string SoftReset = "RESET";
            internal const string DigitalOutputOn = "OUTON";
            internal const string DigitalOutputOff = "OUTOFF";
            internal const string AllDigitalOutputsOn = "ALLON";
            internal const string AllDigitalOutputsOff = "ALLOFF";
            internal const string CaptureImage = "SHOT, 01";//number is parameter bank
            internal const string CancelAndLock = "LOCK";//All reading and scanning operations are forced to stop.
            internal const string Unlock = "UNLOCK";
            internal const string LockStatus = "RLOCK";
            internal const string LaserOn = "AMON";
            internal const string LaserOff = "AMOFF";
            internal const string BeginTuning = "TUNE, 01";//number if reading bank number
            internal const string FinishTuning = "TQUIT";
            internal const string SaveSettings = "SAVE";
            internal const string RevertToDefaultSettings = "DFLT";
            internal const string BusyStatus = "BUSYSTAT";
            internal const string ErrorStatus = "ERRSTAT";



        }

        #endregion


    }
}
