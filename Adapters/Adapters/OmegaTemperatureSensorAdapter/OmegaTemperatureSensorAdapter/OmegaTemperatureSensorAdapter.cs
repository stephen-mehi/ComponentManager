using AdapterBaseClasses;
using CommonServiceInterfaces;
using ComponentInterfaces;
using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.IO.Ports;

namespace OmegaTemperatureSensorAdapter
{
    public class OmegaTemperatureSensorAdapter : SerialAdapter, ISensorAdapter
    {
        public OmegaTemperatureSensorAdapter()
        {
            InitializeMembers();
        }

        public OmegaTemperatureSensorAdapter(
            ICodeContractService codeContractDependency)
            : base(codeContractDependency)
        {
            InitializeMembers();
        }


        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {
            TerminationCharacter = OmegaSpecialCharacters.CommandTerminationCharacter;
            MessageEncoding = Encoding.ASCII;
        }

        private void InitializeMembers()
        {
            TerminationCharacter = OmegaSpecialCharacters.CommandTerminationCharacter;
            MessageEncoding = Encoding.ASCII;
            ComponentName = "Omega Temperature Sensor";
            BaudRate = 9600;
            DataBits = 8;
            StopBits = StopBits.One;
            Handshake = Handshake.None;
            Parity = Parity.None;
            RemoteActionTimeout = 5000;
            WriteTimeout = 500;
            ReadTimeout = 500;
            NumberOfSamplesToAverage = 5;
            InterCharacterDelay = 50;

        }


        #region Properties

        [DataMember]
        [ComponentState]
        [Display]
        public double LastReadTemperature { get; set; }

        [DataMember]
        [ComponentState]
        [Display]
        public int NumberOfSamplesToAverage { get; set; }
        [DataMember]
        [Display]
        public int InterCharacterDelay { get; set; }

        #endregion

        #region EncapsulationMethods

        protected string ConstructFormattedCommand(string command)
        {
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(command),
                "Failed to construct formated command. The command text cannot be empty. Device: " + ComponentName);

            string formattedCommand = command + TerminationCharacter;

            return formattedCommand;
        }

        protected string ConstructFormattedCommand(string command, string parameter)
        {
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(command),
                "Failed to construct formatted command. The command text cannot be empty. Device: " + ComponentName);
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(parameter),
                "Failed to construct formatted command. The parameter text cannot be empty. Device: " + ComponentName);

            string formmattedCommand = command + parameter + TerminationCharacter;

            return formmattedCommand;
        }


        protected bool IsSuccessResponse(string response)
        {
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(response),
                "Failed to evaluate success of response. The response text being checked cannot be empty. Device: " + ComponentName);

            bool isSuccess = response.Contains(OmegaSpecialCharacters.SuccessResponseCharacter);

            return isSuccess;
        }

        protected double ParseTemperatureFromString(string temperatureText)
        {

            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(temperatureText),
                "Failed to parse temperature from string. The temperature text being parsed cannot be empty. Device: " + ComponentName);

            _codeContractDependency.Requires<ArgumentException>(
                temperatureText.Contains(OmegaSpecialCharacters.CommandTerminationCharacter),
                "Failed to parse temperature from string. The temperature text: " +
                temperatureText +
                " did not contain the expected termination character: " +
                OmegaSpecialCharacters.CommandTerminationCharacter +
                "Device: " + ComponentName);

            _codeContractDependency.Requires<ArgumentException>(
                temperatureText.Contains(OmegaSpecialCharacters.MessageDataDelimiter),
                "Failed to parse temperature from string. The temperature text: " +
                temperatureText +
                " did not contain the expected data delimiter: " +
                OmegaSpecialCharacters.MessageDataDelimiter +
                "Device: " + ComponentName);

            //split message by delimiter
            string[] splitMessage = temperatureText.Split(new string[] { OmegaSpecialCharacters.MessageDataDelimiter }, StringSplitOptions.RemoveEmptyEntries);

            _codeContractDependency.Requires<ArgumentException>(
                splitMessage.Length == 2,
                "Failed to parse temperature from string. Incorrect number of sections after splitting temperature text. Device: " + ComponentName);

            //get temperature portion of text
            string tempText = splitMessage[1].Trim(new char[] { ' ', ';', '\r' });
            //parse temperature text
            double tempNumber = double.Parse(tempText);

            return tempNumber;

        }

        public double ReadTemperature()
        {
            double temp;

            string commandText = ConstructFormattedCommand(OmegaCommands.GetCurrentTemperature);

            //if non zero number specified for averaging
            if (NumberOfSamplesToAverage > 1)
            {
                double averagedTemp;
                double summedTemps = 0;

                //loop that many times
                for (int i = 0; i < NumberOfSamplesToAverage; i++)
                {
                    //fetch current temp
                    string currentTemp;

                    Write(commandText);

                    currentTemp = Read(ReadTimeout);

                    //parse temp
                    double currentTempNumber = ParseTemperatureFromString(currentTemp);

                    //add to running sum of temps
                    summedTemps += currentTempNumber;
                }

                //divide sum by n readings
                averagedTemp = summedTemps / NumberOfSamplesToAverage;
                temp = averagedTemp;
            }
            else
            {

                Write(commandText);

                string tempFromSensor = Read(ReadTimeout);

                //parse temp
                double currentTempNumber = ParseTemperatureFromString(tempFromSensor);
                temp = currentTempNumber;
            }

            LastReadTemperature = temp;

            return LastReadTemperature;
        }

        protected void SetUnitsToCelcius()
        {
            string commandText = ConstructFormattedCommand(OmegaCommands.SetCelsius);
            Write(commandText);
            string response = Read(ReadTimeout);
            bool isSuccess = IsSuccessResponse(response);

            _codeContractDependency.Requires<ArgumentException>(
                isSuccess,
                "Failed to set to celcius units. Message sent: " +
                commandText +
                ". Device: " +
                ComponentName);

        }

        protected void SetUnitsToFahrenheit()
        {
            string commandText = ConstructFormattedCommand(OmegaCommands.SetFahrenheit);
            Write(commandText);
            string response = Read(ReadTimeout);
            bool isSuccess = IsSuccessResponse(response);

            _codeContractDependency.Requires<ArgumentException>(
                isSuccess,
                "Failed to set to celcius units. Message sent: " +
                commandText +
                ". Device: " +
                ComponentName);
        }

        protected void StopContinuousDataTransmission()
        {
            string commandText = ConstructFormattedCommand(OmegaCommands.StopContinuousDataTransmission);
            Write(commandText);
            string response = Read(ReadTimeout);
            bool isSuccess = IsSuccessResponse(response);

            _codeContractDependency.Requires<ArgumentException>(
                isSuccess,
                "Failed to set to celcius units. Message sent: " +
                commandText +
                ". Device: " +
                ComponentName);
        }

        #endregion


        #region ISensorAdapter
        [ComponentAction(
            memberAlias: "Read Temperature",
            memberDescription: "Read the temperature in celsius",
            memberId: "_scan",
            isIndependent: false)]
        public string Scan()
        {
            return ReadTemperature().ToString();
        }

        #endregion


        #region OverridingAdapterBase


        public override void CommitConfiguredState()
        {
            return;
        }

        public override void Initialize()
        {

            SetUnitsToCelcius();
            StopContinuousDataTransmission();

        }

        public override string GetError()
        {
            return "Device not capable of reporting errors";
        }

        public override void Pause()
        {
            return;
        }

        public override void Resume()
        {
            return;
        }

        public override void Stop()
        {
            return;
        }

        public override void ShutDown()
        {
            Disconnect();
        }

        public override void ReadState()
        {
            //LastReadTemperature = ReadTemperature();
        }

        public override void Reset()
        {
            Disconnect();
            Connect();
        }


        #endregion


        #region OverridingSerialBase

        protected override void Write(string message)
        {
            //ensure message non null
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(message),
                "Write failed. message cannot be empty. Device: " + ComponentName);

            _codeContractDependency.Requires<ArgumentNullException>(
                MessageEncoding != null,
                "write failed. Encoding cannot be null. Device: " + ComponentName);

            //get bytes of message based on encoding
            byte[] bytesToWrite = MessageEncoding.GetBytes(message);

            //iterate over bytes
            foreach (byte b in bytesToWrite)
            {
                //write single byte
                SerialPortObject.BaseStream.WriteByte(b);
                //sleep to allow device to read that byte
                Thread.Sleep(InterCharacterDelay);
            }

        }


        protected override bool IsConnectionConfirmed()
        {
            string response = string.Empty;
            bool isSuccessful = false;

            Write(ConstructFormattedCommand(OmegaCommands.GetCurrentTemperature));

            try
            {
                response = Read(ReadTimeout);
                isSuccessful = IsSuccessResponse(response);
            }
            catch (TimeoutException)
            {
                //swallow timeout exception on read
            }


            return isSuccessful;

        }

        #endregion


    }


    internal static class OmegaSpecialCharacters
    {
        internal static string CommandTerminationCharacter = "\r";
        internal static string SuccessResponseCharacter = ";";
        internal static string MessageDataDelimiter = ":";

    }

    internal static class OmegaCommands
    {
        internal static string GetCurrentTemperature = "IR";
        internal static string SetCelsius = "F0";
        internal static string SetFahrenheit = "F1";
        internal static string StopContinuousDataTransmission = "P";
        internal static string StartContinuousDataTransmission = "T";
    }
}
