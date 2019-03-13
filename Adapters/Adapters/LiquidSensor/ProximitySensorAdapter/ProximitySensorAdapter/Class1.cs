using ComponentInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Phidget22;

namespace ProximitySensorAdapter
{
    public class ProximitySensorAdapter : IComponentAdapter
    {
        VoltageInput ps;

        public ProximitySensorAdapter()
        {
            ComponentName = "ProximitySensorAdapter";
        }

        #region Properties

        public bool IsDetected { get; set; }
        private double ThresholdVoltage = 1;

        #endregion

        #region Private Methods

        private bool Detected()
        {
            if (ps.Voltage > ThresholdVoltage)
                return true;
            else
                return false;
        }

        #endregion

        #region Public Methods

        public bool IsObjectDetected()
        {
            return Detected();
        }

        #endregion

        #region Implementation_IComponentAdapter

        public string ComponentName { get; set; }

        //not imp
        public void CommitConfiguredState()
        {
            return;
        }

        public void Connect()
        {
            Net.EnableServerDiscovery(ServerType.DeviceRemote);
            ps.DeviceSerialNumber = 370756;
            ps.HubPort = 0;
            ps.Channel = 0;
            ps.IsHubPortDevice = true;
            ps.IsRemote = true;
            ps.Open(5000);
        }

        public void Disconnect()
        {
            ps.Close();
        }

        //close and disconnect and clean up any other things
        public void Dispose()
        {
            ps.Close();
        }

        //maybe
        public string GetError()
        {
            throw new NotImplementedException();
        }

        //not imp
        public void Initialize()
        {
            return;
        }

        //not imp
        public void InjectServiceProvider(IServiceProvider servProv)
        {
            return;
        }


        public bool IsConnected()
        {
            if (ps.Attached)
                return true;
            else
                return false;
        }

        //not imp
        public void Pause()
        {
            return;
        }

        public void ReadState()
        {
            IsDetected = Detected();
        }

        //not imp
        public void Reset()
        {
            return;
        }

        //not imp
        public void Resume()
        {
            return;
        }

        //not imp
        public void ShutDown()
        {
            return;
        }

        //not imp
        public void Stop()
        {
            return;
        }

        #endregion

    }
}