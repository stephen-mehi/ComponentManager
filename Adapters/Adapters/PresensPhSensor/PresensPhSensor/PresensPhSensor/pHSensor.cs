using AdapterBaseClasses;
using CommonServiceInterfaces;
using ComponentInterfaces;
using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;

namespace PresensPhSensor
{
    public class pHSensor : SerialAdapter, IpHSensorAdapter
    {

        protected pHSensor() : base()
        {
            InitializeMembers();
        }

        public pHSensor(
            ICodeContractService codeContractDependency)
            : base(codeContractDependency)
        {
            InitializeMembers();

        }


        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {
            TerminationCharacter = PresensSpecialCharacters.CommandTerminationCharacter;
            MessageEncoding = Encoding.ASCII;
        }

        private void InitializeMembers()
        {
            TerminationCharacter = PresensSpecialCharacters.CommandTerminationCharacter;
            MessageEncoding = Encoding.ASCII;
            ComponentName = "Presens pH Sensor";
            RemoteActionTimeout = 5000;
            ReadTimeout = 750;
            WriteTimeout = 750;
            StopBits = StopBits.One;
            DataBits = 8;
            BaudRate = 19200;
            Handshake = Handshake.None;
            Parity = Parity.None;
            NumberOfSamplesToAverage = 3;
        }

        [ComponentAction(
            memberAlias: "Read pH",
            memberDescription: "Read pH",
            memberId: "_read",
            isIndependent: false)]
        public string Scan()
        {
            SetCompensationTemperature(CompensationTemperature);
            return ReadAveragePh().ToString();
        }



        #region Properties


        [DataMember]
        [ComponentState]
        [Display]
        public double CompensationTemperature { get; set; }

        [ComponentState]
        [DataMember]
        [Display]
        public double LastReadPh { get; set; }
        public string LastReadPhRaw { get; set; }

        [DataMember]
        [Display]
        public int NumberOfSamplesToAverage { get; set; }

        [DataMember]
        [ComponentState]
        [Display]
        public double StandardDeviation { get; set; }

        #endregion

        #region EncapsulationMethods

        protected void SetContinuousMode()
        {
            Write(ConstructFormattedCommand(PresensCommands.ContinuousMode));
            string response = Read(RemoteActionTimeout);
        }

        protected void SetSleepMode()
        {
            Write(ConstructFormattedCommand(PresensCommands.SleepMode));
            string response = Read(RemoteActionTimeout);
            //have to sleep to allow device to go into sleep mode
            Thread.Sleep(1000);

        }

        protected void SetSamplingRate(string rate)
        {
            _codeContractDependency.Requires<ArgumentOutOfRangeException>(
                !string.IsNullOrEmpty(rate),
                "Failed to set sampling rate. Rate string cannot be empty. Device: " +
                ComponentName);

            _codeContractDependency.Requires<ArgumentOutOfRangeException>(
                rate.Length == 4,
                "Failed to set sampling rate. The rate string should be 4 characters. Recieved string: " +
                rate +
                ". Device: " +
                ComponentName);

            Write(ConstructFormattedCommand(PresensCommands.SamplingRatePrefix, rate));
            string response = Read(RemoteActionTimeout);
        }

        protected void SetCompensationTemperature(double temperature)
        {
            //convert temp to formatted string
            string formattedTemp = ConvertTemperatureToFormattedString(temperature);
            //construct full formatted command text
            string commandText = ConstructFormattedCommand(PresensCommands.CompensationTemperaturePrefix, formattedTemp);
            //write to device
            Write(commandText);
            string response = Read(RemoteActionTimeout);
            bool isSuccessful = IsSuccessResponse(response);

            _codeContractDependency.Requires<ArgumentException>(
                isSuccessful,
                "Failed to set compensation temperature. Response: " +
                response +
                ". Device: " +
                ComponentName);
        }

        protected string ConvertTemperatureToFormattedString(double temperature)
        {

            //FORMATTING: 'tmpc0600' FOR 60.0C
            //Initialize leading zeros string
            string leadingZeros = string.Empty;
            string leadingZeroString = "0";
            //only 3 chars allowed in message, not 4 because one trailing zero for tens place
            int nAllowedChars = 3;
            int nTotalMessageLength = 4;
            string trailingZero = leadingZeroString;

            //round up and cast to int
            int roundedTemp = (int)Math.Ceiling((decimal)temperature);
            //convert to string
            string temperatureString = roundedTemp.ToString();

            //confirm not longer than allowed chars
            _codeContractDependency.Requires<ArgumentOutOfRangeException>(
                temperatureString.Length <= nAllowedChars,
                "Failed to convert temp to formatted string. Too many digits in number: " +
                temperature +
                ". Device: " +
                ComponentName);

            //calculate required leading zeros
            int requiredLeadingZeros = nAllowedChars - temperatureString.Length;

            //build up leading zero string
            for (int i = 0; i < requiredLeadingZeros; i++)
                leadingZeros += leadingZeroString;

            //add leading/trailing zero
            string formattedTemperatureString = leadingZeros + roundedTemp.ToString() + trailingZero;

            _codeContractDependency.Requires<ArgumentOutOfRangeException>(
                formattedTemperatureString.Length == nTotalMessageLength,
                "Failed to convert temp to formatted string. Too many digits in formatted string: " +
                formattedTemperatureString +
                ". Device: " +
                ComponentName);

            return formattedTemperatureString;

        }

        protected double CalculateStdDev(IEnumerable<double> values)
        {
            if (values == null || values.Count() == 0)
                throw new ArgumentNullException("Failed to calculate standard deviation. Collection of values cannot be null. Device: " + ComponentName);

            double ret = 0;

            if (values.Count() > 0)
            {
                //Compute the Average      
                double avg = values.Average();
                //Perform the Sum of (value-avg)^2
                double sum = values.Sum(d => Math.Pow(d - avg, 2));
                //square the [sum divided by the [count-1]]
                ret = Math.Sqrt((sum) / (values.Count() - 1));
            }

            return ret;
        }

        protected double ReadSinglePh()
        {

            string commandText = ConstructFormattedCommand(PresensCommands.DataRequest);
            //write command to sensor
            Write(commandText);
            //read echo confirmation
            string echoResponse = Read(ReadTimeout);
            //check that command was accepted
            bool isCommandAccepted = IsSuccessResponse(echoResponse);

            //confirm or throw
            _codeContractDependency.Requires<ArgumentException>(
                isCommandAccepted,
                "Failed to read pH. The command: " +
                commandText +
                " was not acknowledged as valid by the device. Device: " +
                ComponentName);

            //read data string from sensor
            string dataResponse = Read(RemoteActionTimeout);
            //set last raw ph prop
            LastReadPhRaw = dataResponse;
            //parse pH value
            double pH = ParsePhFromResponse(dataResponse);
            //set last read ph prop
            LastReadPh = pH;
            //standard deviation to invalid since 1 value
            StandardDeviation = 0;

            return pH;
        }

        protected double ReadAveragePh()
        {

            //declare return var
            double pH;

            //if n samples to average greater than 0
            if (NumberOfSamplesToAverage > 1)
            {

                //init req vars
                double averagedPh = 0;
                double summedPh = 0;
                var phVals = new List<double>();

                //attempt to set to continuous and parse input pH vals
                try
                {
                    //at fastest rate
                    SetSamplingRate("0000");
                    //read continuously 
                    SetContinuousMode();

                    //iterate specified times
                    for (int i = 0; i < NumberOfSamplesToAverage; i++)
                    {
                        double currentPh = ParsePhFromResponse(Read(RemoteActionTimeout));
                        phVals.Add(currentPh);
                        summedPh += currentPh;
                    }

                }
                //always set to sleep mode before exiting
                finally
                {
                    //set sleep mode
                    SetSleepMode();
                    //discard any extra pH data in recieve buffer
                    SerialPortObject.DiscardInBuffer();
                }

                //standard dev
                double standardDev = CalculateStdDev(phVals);
                //average
                averagedPh = summedPh / NumberOfSamplesToAverage;

                //set ret val
                pH = averagedPh;
                //set last read pH
                LastReadPh = pH;
                //set last standard dev
                StandardDeviation = standardDev;

            }
            else
            {
                //just read single
                pH = ReadSinglePh();
            }


            return pH;

        }

        protected double ParsePhFromResponse(string responseText)
        {

            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(responseText),
                "Failed to parse ph from text. Text cannot be empty. Device: " +
                ComponentName);

            string parsedPhString = ParsePhStringFromResponse(responseText);
            double phNumeric = ParsePhHelper(parsedPhString);

            return phNumeric;

        }

        protected string ParsePhStringFromResponse(string responseText)
        {
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(responseText),
                "Failed to parse ph from text. Text cannot be empty. Device: " +
                ComponentName);

            //split response text by semicolons
            string[] splitText = responseText.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            _codeContractDependency.Requires<IndexOutOfRangeException>(
                splitText.Length == 6, "Failed to parse ph from text. Text : " +
                responseText +
                " did not contain expected number of sections. Device: " +
                ComponentName);

            string phSection =
                splitText[3]?//select third element, which should hold pH info
                .Trim(new char[] { ' ', 'H' })?//trim any whitespace and the 'H' prefix
                .TrimStart(new char[] { '0' });//trim any leading zeros

            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(phSection),
                "Failed to parse pH. Raw ph text: " +
                responseText +
                ". Device: " +
                ComponentName);

            return phSection;
        }

        protected double ParsePhHelper(string phString)
        {
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(phString),
                "Failed to parse ph from text. Text cannot be empty. Device: " +
                ComponentName);

            //cast to double and adjust decimal place
            double phNumeric = double.Parse(phString) / 100;
            //init valid ph range
            int phMax = 15;
            int phMin = 0;

            _codeContractDependency.Requires<ArgumentOutOfRangeException>(
                phNumeric < phMax && phNumeric > phMin,
                "Parsed pH, but value: " +
                phNumeric +
                " was out of range. Max: " +
                phMax +
                " Min: " +
                phMin +
                ". Device: " +
                ComponentName);

            return phNumeric;
        }

        protected string ConstructFormattedCommand(string command)
        {
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(command),
                "Failed to construct formatted command text. Command text cannot be null. Device: " +
                ComponentName);

            string formattedCommand = command + TerminationCharacter;

            return formattedCommand;
        }

        protected string ConstructFormattedCommand(string command, string parameter)
        {
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(command),
                "Failed to construct formatted command text. Command text cannot be empty. Device: " +
                ComponentName);

            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(parameter),
                "Failed to construct formatted command text. Parameter text cannot be empty. Device: " +
                ComponentName);

            _codeContractDependency.Requires<ArgumentNullException>(
                parameter.Length == 4,
                "Failed to construct formatted command text. Parameter text not correct length. Device: " +
                ComponentName);

            string formmattedCommand = command + parameter + TerminationCharacter;

            return formmattedCommand;
        }

        protected bool IsSuccessResponse(string response)
        {
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(response),
                "Failed to evaluate success of response text. The response being checked cannot be empty. Device: " +
                ComponentName);

            return response.Contains(PresensSpecialCharacters.SuccessResponseCharacter);
        }

        protected void ConfigureCommunicationSettings()
        {
            //set echo mode on
            Write(ConstructFormattedCommand(PresensCommands.EchoModeOn));
            //read response
            Read(ReadTimeout);
            //stop data from streaming in by putting in sleep mode
            Write(ConstructFormattedCommand(PresensCommands.SleepMode));
            //sleep to allow device to go into sleep mode
            Thread.Sleep(3000);
            //read response
            Read(RemoteActionTimeout);
            //set sampling rate
            Write(ConstructFormattedCommand(PresensCommands.SamplingRatePrefix, "0100"));
            //read response
            Read(ReadTimeout);
            //discard any data that may have streamed in
            SerialPortObject.DiscardInBuffer();
            //set echo mode to enhanced
            Write(ConstructFormattedCommand(PresensCommands.EnhancedEcho));
            //read response
            string response = Read(ReadTimeout);
            //judge success
            bool isSuccess = IsSuccessResponse(response);

            _codeContractDependency.Requires<ArgumentException>(
                isSuccess,
                "Failed to configure initial communication settings. Raw response: " + response + " Device: " +
                ComponentName);

        }




        #endregion

        #region OverridingAdapterBase

        public override void CommitConfiguredState()
        {
            return;
        }


        [ComponentAction(
            memberAlias: "Initialize",
            memberDescription: "Initialize pH sensor and temp sensor",
            memberId: "_initialize",
            isIndependent: false)]
        public override void Initialize()
        {

            SetSleepMode();
            CompensationTemperature = 25;
            SetCompensationTemperature(CompensationTemperature);

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
            SetSleepMode();
            return;
        }

        [ComponentAction(
            memberAlias: "Shut down",
            memberDescription: "Put sensors in sleep mode and disconnect",
            memberId: "_shutDown",
            isIndependent: false)]
        public override void ShutDown()
        {
            Disconnect();
        }

        public override void ReadState()
        {
            //LastReadPhRaw = Scan().ToString();

        }

        [ComponentAction(
            memberAlias: "Reset",
            memberDescription: "Reset sensors",
            memberId: "_reset",
            isIndependent: false)]
        public override void Reset()
        {
            Disconnect();
            Connect();
        }



        #endregion

        #region OverridingSerialBase

        public override void Disconnect()
        {
            base.Disconnect();
        }


        protected override bool IsConnectionConfirmed()
        {
            string echoResponse = string.Empty;
            bool isSuccessful = false;

            try
            {
                //set echo modes and sampling rate
                ConfigureCommunicationSettings();
                //success if made it here
                isSuccessful = true;
            }
            catch (TimeoutException)
            {
                //swallow timeout exception on read
            }

            return isSuccessful;

        }

        #endregion


    }

    internal static class PresensSpecialCharacters
    {
        internal const string CommandTerminationCharacter = "\r";
        internal const string FailureResponseCharacter = "?";
        internal const string SuccessResponseCharacter = "!";

    }

    internal static class PresensCommands
    {
        internal const string ContinuousMode = "mode0000";
        internal const string SleepMode = "mode0001";
        internal const string MuxSerialMode = "mode0002";
        internal const string MuxConfigMode = "mode0004";
        internal const string SynchronousMode = "mode0005";
        internal const string DataRequest = "data";
        internal const string SamplingRatePrefix = "samp";
        internal const string CompensationTemperaturePrefix = "tmpc";
        internal const string EchoModeOn = "echo0001";
        internal const string EchoModeOff = "echo0000";
        internal const string SimpleEcho = "view0007";
        internal const string EnhancedEcho = "view0000";

    }






}
