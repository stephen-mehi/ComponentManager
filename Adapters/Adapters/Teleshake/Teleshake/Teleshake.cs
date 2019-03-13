using AdapterBaseClasses;
using ComponentInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace InhecoShakerComponent
{
    public class Teleshake : IComponentAdapter
    {

        public Teleshake()
        {
            ComponentName = "Teleshake";

            ErrorLibrary = new Dictionary<int, string>()
            {
                { -1, "Error not set"},
                { 101, "Already in commanded state"},
                { 201, "Error bit flagged in response"},
                { 202, "Dirty bit flagged in response"},
                { 203, "No memory error"},
                { 204, "List empty error"},
                { 205, "Wrong password error"},
                { 206, "Not initialized error"},
                { 207, "Already initialized error"},
                { 208, "Device not attached error"},
                { 209, "Parameter out of range error"},
                { 210, "Unknown, check documentation"},
                { 300, "General communications error"},
                { 301, "Error initializing communications"},
                { 302, "Communication shut down"},
                { 303, "Error opening port"},
                { 304, "Error closing port"},
                { 305, "CRC error"},
                { 306, "CRC error"},
                { 307, "Error writing to port"},
                { 308, "Error reading from port"},
                { 309, "Communications not initialized"},
                { 310, "Port not open"},
                { 311, "Unknown check documentation"},
                { 404, "Unable to find cmdlib.dll library"}
            };
        }



        #region Fields

        private Dictionary<int, string> ErrorLibrary = new Dictionary<int, string>()
        {
            { 201, "Error bit flagged in response"},
            { 202, "Dirty bit flagged in response"},
            { 203, "No memory error"},
            { 204, "List empty error"},
            { 205, "Wrong password error"},
            { 206, "Not initialized error"},
            { 207, "Already initialized error"},
            { 208, "Device not attached error"},
            { 209, "Parameter out of range error"},
            { 210, "Unknown, check documentation"},
            { 300, "General communications error"},
            { 301, "Error initializing communications"},
            { 302, "Communication shut down"},
            { 303, "Error opening port"},
            { 304, "Error closing port"},
            { 305, "CRC error"},
            { 306, "CRC error"},
            { 307, "Error writing to port"},
            { 308, "Error reading from port"},
            { 309, "Communications not initialized"},
            { 310, "Port not open"},
            { 311, "Unknown check documentation"},
        };

        private const string libraryVersion = "01.00.00";

        #endregion

        #region PROPERTIES

        [DataMember]
        [Display]
        public tPortNum PortName { get; set; }
        [DataMember]
        [Display]
        public tDeviceAddress DeviceAddress { get; set; }


        #endregion

        #region Encapsulation_Methods

        protected string GetErrorMessage(int errorCode)
        {
            string errorMessage = string.Empty;

            if (errorCode == default(int))
            {
                errorMessage = "No error";
            }
            else
            {

                bool foundError = ErrorLibrary.TryGetValue(key: errorCode, value: out errorMessage);

                if (!foundError)
                {
                    errorMessage = "No error message found for error code: " + errorCode;
                }
            }

            return errorMessage;

        }

        #endregion

        #region IComponentAdapter_Implementation

        public string ComponentName { get; set; }

        public void CommitConfiguredState()
        {
            //NO LOGIC
        }

        public void Connect()
        {

            string errorPrefix = "Failed to " + nameof(Connect) + " component: " + ComponentName + ". Device address: " + DeviceAddress + ". ";

            int noMemError = 203;
            int nRetries = 10;
            int retVal = -1;

            for (int i = 0; i < nRetries; i++)
            {

                retVal = TeleshakeWrapper.Connect(port: PortName);

                if (retVal != noMemError)
                    break;

                Thread.Sleep(300);
            }

            if (retVal != 0)
                throw new Exception(errorPrefix + GetErrorMessage(retVal));

        }

        public void Disconnect()
        {
            //nothing
        }

        public void Dispose()
        {

            string errorPrefix = "Failed to " + nameof(Dispose) + " component: " + ComponentName + ". Device address: " + DeviceAddress + ". ";
            int noMemError = 203;
            int nRetries = 10;
            int retVal = -1;

            for (int i = 0; i < nRetries; i++)
            {
                retVal = TeleshakeWrapper.Disconnect();

                if (retVal != noMemError)
                    break;

                Thread.Sleep(300);
            }

            if (retVal != 0)
                throw new Exception(errorPrefix + GetErrorMessage(retVal));

        }

        public string GetError()
        {
            return "Note yet implemented";
        }

        public void Initialize()
        {
            //NO LOGIC
        }

        public void InjectServiceProvider(IServiceProvider servProv)
        {
            //NO LOGIC
        }


        public bool IsConnected()
        {

            int retVal = TeleshakeWrapper.Connect(port: PortName);
            bool isConnected = false;

            if (retVal == 0)
                isConnected = true;

            return isConnected;

        }

        public void Pause()
        {
            //nothing

        }

        public void ReadState()
        {
            //NO LOGIC
        }

        public void Reset()
        {
            //nothing
        }

        public void Resume()
        {
            //nothing
        }

        public void ShutDown()
        {
            Stop();
            Disconnect();
        }

        [ComponentAction(memberAlias: "Stop", memberDescription: "Stop the device", memberId: "_stop", isIndependent: false)]
        public void Stop()
        {
            int retVal = -1;
            string errorPrefix = "Failed to " + nameof(Stop) + " component: " + ComponentName + ". Device address: " + DeviceAddress + ". ";
            int noMemError = 203;
            int inCommandedStateError = 101;
            int nRetries = 10;

            for (int i = 0; i < nRetries; i++)
            {
                retVal = TeleshakeWrapper.Stop(port: PortName, devAddress: DeviceAddress);

                if (retVal != noMemError && retVal != inCommandedStateError)
                    break;

                Thread.Sleep(300);
            }


            if (retVal != 0)
                throw new Exception(errorPrefix + GetErrorMessage(retVal));
        }

        #endregion

        #region PUBLIC_METHODS

        [ComponentAction(memberAlias: "Start Shaking", memberDescription: "Start shaking at the specified rpm", memberId: "_startShaking", isIndependent: false)]
        public void Shake(
            [ComponentActionParameter(memberAlias:"RPM", memberDescription:"Rotations per minute", memberId:"_rpm")]
            int rpm)
        {

            string failPrefix = "Failed to start shaking. ";

            #region preconditions

            if (rpm < 0)
                throw new ArgumentOutOfRangeException(nameof(rpm), failPrefix + "rpm cannot be less than 0.");

            if (DeviceAddress == tDeviceAddress.none)
                throw new ArgumentOutOfRangeException(nameof(DeviceAddress), failPrefix + "Device address cannot be none.");

            #endregion

            int noMemError = 203;
            int nRetries = 10;
            int retVal = -1;

            UInt16 castRpm = (UInt16)rpm;

            for (int i = 0; i < nRetries; i++)
            {
                retVal = TeleshakeWrapper.Shake(port: PortName, devAddress: DeviceAddress, rpm: ref castRpm);

                if (retVal != noMemError)
                    break;

                Thread.Sleep(300);
            }

            if (retVal != 0)
                throw new Exception(failPrefix + GetErrorMessage(retVal));

        }

        #endregion

    }
    internal class TeleshakeWrapper
    {
        [DllImport("TeleshakeLibraryWrapper.dll", EntryPoint = "Disconnect", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Disconnect();

        [DllImport("TeleshakeLibraryWrapper.dll", EntryPoint = "Connect", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Connect(tPortNum port);

        [DllImport("TeleshakeLibraryWrapper.dll", EntryPoint = "Stop", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Stop(tPortNum port, tDeviceAddress devAddress);

        [DllImport("TeleshakeLibraryWrapper.dll", EntryPoint = "Shake", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Shake(tPortNum port, tDeviceAddress devAddress, ref UInt16 rpm);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct tDevInfo
    {
        public tDeviceAddress devAddr;
        public byte fwVersionMajor;
        public byte fwVersionMinor;
        public uint serialNumber;
        public byte status;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct tnode
    {
        public tDevInfo deviceInfo;
        public IntPtr next;//pointer to tnode
    }


    public enum tPortNum
    {
        none = 0,
        COM1 = 1,
        COM2 = 2
    }

    public enum tDeviceAddress
    {
        none = 0,
        device01,
        device02,
        device03,
        device04,
        device05,
        device06,
        device07,
        device08,
        device09,
        device10,
        device11,
        device12,
        device13,
        device14
    };


}
