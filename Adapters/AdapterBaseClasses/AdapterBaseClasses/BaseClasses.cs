using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;
using CommonServiceInterfaces;
using ComponentInterfaces;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace AdapterBaseClasses
{

    /// <summary>
    /// Base implementation of ICartesianCoordinates
    /// </summary>
    [DataContract]
    public class CartesianCoordinates : ICartesianCoordinates
    {
        /// <summary>
        /// X coordinate value
        /// </summary>
        [DataMember]
        [Display]
        public int X_Axis { get; set; }

        /// <summary>
        /// Y coordinate value
        /// </summary>
        [DataMember]
        [Display]
        public int Y_Axis { get; set; }

        /// <summary>
        /// Z coordinate value
        /// </summary>
        [DataMember]
        [Display]
        public int Z_Axis { get; set; }
    }


    /// <summary>
    /// Base implementation of ICartesianEndEffectorMetadata
    /// </summary>
    [DataContract]
    public class CartesianEndEffectorMetaData : ICartesianEndEffectorMetadata
    {
        /// <summary>
        /// Component ID
        /// </summary>
        [DataMember]
        [Display]
        public string ComponentID { get; set; }

        /// <summary>
        /// Profile ID
        /// </summary>
        [DataMember]
        [Display]
        public string AdapterProfile { get; set; }

        /// <summary>
        /// Description of component
        /// </summary>
        [DataMember]
        [Display]
        public string Description { get; set; }

        /// <summary>
        /// X axis offset relative to teachpoint for end effector to operate correctly on target. 
        /// </summary>
        [DataMember]
        [Display]
        public int XOffset { get; set; }

        /// <summary>
        /// Y axis offset relative to teachpoint for end effector to operate correctly on target. 
        /// </summary>
        [DataMember]
        [Display]
        public int YOffset { get; set; }

        /// <summary>
        /// Z axis offset relative to teachpoint for end effector to operate correctly on target. 
        /// </summary>
        [DataMember]
        [Display]
        public int ZOffset { get; set; }

        /// <summary>
        /// Human readable name of component
        /// </summary>
        [DataMember]
        [Display]
        public string Name { get; set; }
    }


    /// <summary>
    /// Base abstract implmentation of IComponentAdapter
    /// </summary>
    [DataContract]
    public abstract class ComponentAdapter : IComponentAdapter
    {

        /// <summary>
        /// protected ctor because this is abstract class
        /// </summary>
        protected ComponentAdapter() { }

        /// <summary>
        /// protected ctor because this is abstract class
        /// </summary>
        protected ComponentAdapter(
            ICodeContractService codeConDep)
        {
            _codeContractDependency = codeConDep ?? throw new ArgumentNullException(nameof(codeConDep), "Failed in ctor of component adpater base class. Code contract object cannot be null");
        }

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        [DataMember]
        [Display]
        public string ComponentName { get; set; }


        #endregion

        #region IComponentAdapter

        /// <summary>
        /// Setup routine required to get component in valid initial state
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Error state from component
        /// </summary>
        /// <returns>error state</returns>
        public abstract string GetError();

        /// <summary>
        /// interrupt component and cancel current operation
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// interrupt component and pause current operation
        /// </summary>
        public abstract void Pause();

        /// <summary>
        /// Resume previously pause operation
        /// </summary>
        public abstract void Resume();

        /// <summary>
        /// Interrupt component and reset
        /// </summary>
        public abstract void Reset();

        /// <summary>
        /// Shut down routine required to get component in valid dormant state
        /// </summary>
        public abstract void ShutDown();

        /// <summary>
        /// Connect to component
        /// </summary>
        public abstract void Connect();

        /// <summary>
        /// Disconnect from component
        /// </summary>
        public abstract void Disconnect();

        /// <summary>
        /// Get connection state
        /// </summary>
        public abstract bool IsConnected();

        /// <summary>
        /// Commit any volatile configuration data
        /// </summary>
        public abstract void CommitConfiguredState();

        /// <summary>
        /// Read component state
        /// </summary>
        public abstract void ReadState();

        #endregion

        #region IDisposable

        /// <summary>
        /// Internal disposed state flag
        /// </summary>
        protected bool disposed = false;

        //implemented according to MSDN "Dispose Pattern"

        /// <summary>
        /// Implementation of IDisposable 
        /// </summary>
        public void Dispose()
        {
            //call dispose informing that we are cleaning up from the dispose and not the finalizer
            Dispose(true);
            //prevent finalizer from running
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Handles releasing unmanaged resources before objects death. 
        /// </summary>
        /// <param name="disposing">Determines whether being invoked from within Dispose or a finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            disposed = true;
        }


        /// <summary>
        /// attempts to set all internal injected members and internal service provider ref
        /// </summary>
        public virtual void InjectServiceProvider(IServiceProvider servProv)
        {
            serviceProviderDep = servProv ?? throw new ArgumentNullException("Failed to inject service provider. Service provider object cannot be null");

            _codeContractDependency = (ICodeContractService)servProv.GetService(typeof(ICodeContractService)) ?? throw new FileLoadException(nameof(ICodeContractService), "Failed to inject service provider. Unable to resolve code contract service");
        }


        #endregion

        /// <summary>
        /// protected service provider
        /// </summary>
        protected IServiceProvider serviceProviderDep;


        /// <summary>
        /// Code contract dependency. Used for specifying pre and post conditions within a method
        /// </summary>
        [IgnoreDataMember]
        protected ICodeContractService _codeContractDependency;


    }


    /// <summary>
    /// Base serial device implementation. Provides base tooling for communication with serial devices
    /// </summary>
    [DataContract]
    public abstract class SerialAdapter : ComponentAdapter
    {
        /// <summary>
        /// protected default ctor
        /// </summary>
        protected SerialAdapter() : base()
        {
            InitializeMembers();
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="codeContractDependency"></param>
        protected SerialAdapter(ICodeContractService codeContractDependency) : this()
        {
            //set code contract dep
            if (codeContractDependency == null)
            {
                throw new ApplicationException("");
            }

            _codeContractDependency = codeContractDependency;

        }

        /// <summary>
        /// Runs after deserialization occurs
        /// </summary>
        /// <param name="context"></param>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            InitializeMembers();
        }

        /// <summary>
        /// any initial object setup should occur here. This is invoked after deserialization.
        /// </summary>
        private void InitializeMembers()
        {
            SerialPortObject = new SerialPort();
        }

        
        /// <summary>
        /// The encoding type used when converting strings to bytes before sending to serial device endpoint
        /// </summary>
        public Encoding MessageEncoding { get; set; }

        /// <summary>
        /// internal serial port helper object
        /// </summary>
        protected SerialPort SerialPortObject { get; set; }

        /// <summary>
        /// The character used to specify the end of a message to or from the serial device endpoint
        /// </summary>
        public string TerminationCharacter { get; set; }

        /// <summary>
        /// The port name e.g. COM1
        /// </summary>
        [DataMember]
        [Display]
        public string PortName { get; set; }

        /// <summary>
        /// The time allowed for the device to perform a blocking action before a timeout exception will be thrown
        /// </summary>
        [DataMember]
        [Display]
        public int RemoteActionTimeout { get; set; }

        /// <summary>
        /// The time allowed to write all bytes to the output serial buffer
        /// </summary>
        [DataMember]
        [Display]
        public int WriteTimeout { get; set; }

        /// <summary>
        /// The time allowed to make a blocking read from the serial input buffer
        /// </summary>
        [DataMember]
        [Display]
        public int ReadTimeout { get; set; }

        /// <summary>
        /// The number of bits allocated for data according to the device's specific serial communication specification 
        /// </summary>
        [DataMember]
        [Display]
        public int DataBits { get; set; }

        /// <summary>
        /// The baud rate according to the device's specific serial communication specification 
        /// </summary>
        [DataMember]
        [Display]
        public int BaudRate { get; set; }

        /// <summary>
        /// The number of bits allocated to designate a full message stop according to the device's specific serial communication specification 
        /// </summary>
        [DataMember]
        [Display]
        public StopBits StopBits { get; set; }

        /// <summary>
        /// The type of parity error checking according to the device's specific serial communication specification 
        /// </summary>
        [DataMember]
        [Display]
        public Parity Parity { get; set; }

        /// <summary>
        /// The type of hardware handshake mechanism - using DTR, DSR, RTS, and CTS - according to the device's specific serial communication specification 
        /// </summary>
        [DataMember]
        [Display]
        public Handshake Handshake { get; set; }


        #region EncapsulationMethods

        /// <summary>
        /// Should provide logic for confirming the validity of a serial connection during port scanning and binding. 
        /// During port scanning, all available ports are iterated, bound, and then this check is run against the port
        /// to confirm it is the correct device.
        /// </summary>
        /// <returns>Flag indicating validity of connection</returns>
        protected abstract bool IsConnectionConfirmed();

        /// <summary>
        /// Attempt to connect to port with current serial connection configuration
        /// </summary>
        /// <param name="portName">Name of the port where a connection should be attempted</param>
        /// <returns></returns>
        protected bool TryConnectWithCurrentSettings(string portName)
        {
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(portName), "Failed to connect to device. Port name cannot be empty. Device: " + ComponentName);

            bool isConfirmedConnection = false;

            //init temp port obj
            SerialPortObject = new SerialPort(portName, BaudRate, Parity, DataBits, StopBits);
            SerialPortObject.Handshake = Handshake;
            SerialPortObject.Encoding = MessageEncoding;
            SerialPortObject.WriteTimeout = WriteTimeout;
            SerialPortObject.ReadTimeout = ReadTimeout;

            try
            {
                //open connection
                SerialPortObject.Open();
            }
            catch (Exception)
            {
                //swallow exception if fails to open with current port name
                //close if connection not confirmed
                if (SerialPortObject.IsOpen)
                    SerialPortObject.Close();

                SerialPortObject.Dispose();
            }

            //if is open 
            if (SerialPortObject.IsOpen)
            {
                isConfirmedConnection = IsConnectionConfirmed();

                //if connection is not confirmed
                if (isConfirmedConnection)
                {
                    //set port name to it
                    PortName = portName;
                }
                else
                {
                    //close if connection not confirmed
                    SerialPortObject.Close();
                    SerialPortObject.Dispose();
                }
            }

            return isConfirmedConnection;
        }

        /// <summary>
        /// Iterate over available ports and attempt connection with currently configured serial settings
        /// </summary>
        protected void ScanPortsAndConnectWithCurrentSettings()
        {
            //get port names
            string[] ports = SerialPort.GetPortNames();

            //iterate over them
            foreach (string portName in ports)
            {
                //if connection made
                if (TryConnectWithCurrentSettings(portName))
                    break;
            }

            _codeContractDependency.Requires<InvalidOperationException>(SerialPortObject.IsOpen, "Failed to scan and bind port with the current connection settings. Device: " + ComponentName);

        }

        /// <summary>
        /// Write message bytes to serial port
        /// </summary>
        /// <param name="message">text to write to port</param>
        protected virtual void Write(string message)
        {
            _codeContractDependency.Requires<NullReferenceException>(IsConnected(), "Failed to write because device not connected. Device: " + ComponentName);
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), "Failed to write. The message cannot be empty. Device: " + ComponentName);

            SerialPortObject.Write(message);

        }

        /// <summary>
        /// Read up to configured termination character
        /// </summary>
        /// <param name="timeout">Time to block and read until timeout exception is thrown</param>
        /// <returns></returns>
        protected virtual string Read(int timeout)
        {

            #region CodeContractPreconditions
            _codeContractDependency.Requires<InvalidOperationException>(timeout > 0, "Failed to read. Timeout must be greater than 0. Device: " + ComponentName);
            _codeContractDependency.Requires<InvalidOperationException>(IsConnected(), "Failed to read. Device not connected. Device: " + ComponentName);
            _codeContractDependency.Requires<InvalidOperationException>(!string.IsNullOrEmpty(TerminationCharacter), "Failed to read. Device has no configured message termination character. Device: " + ComponentName);
            #endregion

            string readMessage = "Unknown";
            string message = string.Empty;

            var memStream = new MemoryStream();

            byte[] buffer = new byte[1];

            //update serial port
            SerialPortObject.ReadTimeout = ReadTimeout;
            SerialPortObject.Encoding = MessageEncoding;


            //init timeout mechanism
            Stopwatch timeoutWatch = new Stopwatch();
            timeoutWatch.Start();

            //continue reading until message termination character found
            while (!MessageEncoding.GetString(memStream.ToArray()).Contains(TerminationCharacter))
            {
                //if data is available
                if (SerialPortObject.BytesToRead > 0)
                {
                    //read byte and place in array
                    buffer[0] = (byte)SerialPortObject.ReadByte();

                    //write to memory stream
                    memStream.Write(buffer, 0, 1);
                }
                else
                {
                    Thread.Sleep(10);
                }

                //timeout if too much time passed
                _codeContractDependency.Requires<TimeoutException>(timeoutWatch.ElapsedMilliseconds < timeout, "Failed to read. Timed out. Device: " + ComponentName);

            }

            readMessage = MessageEncoding.GetString(memStream.ToArray());

            return readMessage;

        }

        #endregion

        #region OverridingAdapterBase

        

        /// <summary>
        /// Attempt connection with current port name setting. 
        /// If that fails, attempt connection on all other ports
        /// </summary>
        public override void Connect()
        {
            //if port name null or connection to the configured port name failed
            if (string.IsNullOrEmpty(PortName) || !TryConnectWithCurrentSettings(PortName))
            {
                //attempt automated port scan and connection
                ScanPortsAndConnectWithCurrentSettings();
            }

        }

        /// <summary>
        /// Disconnect from serial port 
        /// </summary>
        public override void Disconnect()
        {

            #region CodeContractPreconditions
            _codeContractDependency.Requires<ObjectDisposedException>(!disposed, "Failed to disconnect. Object has already been disposed. Device: " + ComponentName);
            #endregion

            //ifport is open
            if (SerialPortObject.IsOpen)
            {
                //close it
                SerialPortObject.Close();
            }

            SerialPortObject.Dispose();

        }

        /// <summary>
        /// Check the connection state of the port
        /// </summary>
        /// <returns>The connection state of the port</returns>
        public override bool IsConnected()
        {
            return SerialPortObject.IsOpen;
        }


        #endregion

        #region IDisposable

        /// <summary>
        /// Handles releasing resources before objects death 
        /// </summary>
        /// <param name="disposing">Determines whether being invoked from within Dispose or a finalizer</param>
        protected override void Dispose(bool disposing)
        {
            //only release resources if this object has not alread been disposed
            if (!disposed)
            {
                //release resources if invoked from within Dispose
                if (disposing)
                {
                    if (SerialPortObject != null)
                    {
                        if (SerialPortObject.IsOpen)
                        {
                            SerialPortObject.Close();
                        }

                        SerialPortObject.Dispose();

                        SerialPortObject = null;
                    }
                }
            }

        }

        #endregion

    }

    /// <summary>
    /// Base TCP device implementation. Provides base tooling for communication with TCP IP devices
    /// </summary>
    [DataContract]
    public abstract class TCP_IP_Adapter : ComponentAdapter
    {

        /// <summary>
        /// default ctor
        /// </summary>
        protected TCP_IP_Adapter() : base()
        {
            InitializeMembers();
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="codeContractDependency"></param>
        protected TCP_IP_Adapter(ICodeContractService codeContractDependency) : this()
        {
            //set code contract dep
            if (codeContractDependency == null)
            {
                throw new ApplicationException("");

            }
            _codeContractDependency = codeContractDependency;

        }


        /// <summary>
        /// Runs directly after deserialization occurs. 
        /// </summary>
        /// <param name="context"></param>
        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {
            InitializeMembers();
        }

        /// <summary>
        /// Object state gets initialized here. Gets invoked after deserialization.
        /// </summary>
        private void InitializeMembers()
        {
            TcpObject = new TcpClient();
        }

        #region PROPS

        /// <summary>
        /// The time allowed for the device to perform a blocking action before a timeout exception will be thrown
        /// </summary>
        [DataMember]
        [Display]
        public int RemoteActionTimeout { get; set; }

        /// <summary>
        /// The amount of time to make a blocking read before throwing a timout exception
        /// </summary>
        [DataMember]
        [Display]
        public int ReadTimeout { get; set; }

        /// <summary>
        /// The amount of time to make a blocking write before timing out
        /// </summary>
        [DataMember]
        [Display]
        public int WriteTimeout { get; set; }

        /// <summary>
        /// internal tcp helper object
        /// </summary>
        protected TcpClient TcpObject { get; set; }

        /// <summary>
        /// Encoding used when converting strings to bytes before writing to network stream 
        /// </summary>
        public Encoding MessageEncoding { get; set; }

        /// <summary>
        /// The IP address of the component
        /// </summary>
        [DataMember]
        [Display]
        public string IpAddress { get; set; }

        /// <summary>
        /// Network port the device listens on 
        /// </summary>
        [DataMember]
        [Display]
        public int Port { get; set; }

        /// <summary>
        /// Collection of possible termination characters. 
        /// Used for determining the end of an incoming message.
        /// </summary>
        public string[] TerminationCharacters { get; set; }

        #endregion

        #region IComponentAdapter

        /// <summary>
        /// Checks TCP object connection 
        /// </summary>
        /// <returns>returns connection state of TCP object</returns>
        public override bool IsConnected()
        {
            #region CodeContractPreconditions
            _codeContractDependency.Requires<ObjectDisposedException>(!disposed, "Failed to check connection state of device. Object has already been disposed. Please create a new instance to operate on it. Device: " + ComponentName);
            _codeContractDependency.Requires<NullReferenceException>(TcpObject != null, "Failed to check connection state of device. Connection object was null. Device: " + ComponentName);
            #endregion

            bool isConnected = TcpObject.Connected;

            return isConnected;

        }


        /// <summary>
        /// Connect to component
        /// </summary>
        public override void Connect()
        {

            #region CodeContractPreconditions
            _codeContractDependency.Requires<ObjectDisposedException>(!disposed, "Failed to connect to device. Object already disposed. Device: " + ComponentName);
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(IpAddress), "Failed to connect. Ip address cannot be empty. Device: " + ComponentName);
            #endregion

            if (TcpObject == null)//if tcp client was disposed, create a new instance
            {
                TcpObject = new TcpClient();
            }
            else//if it isnt disposed, check if its connected
            {
                if (TcpObject.Connected)//Disconnect if it is already connected
                {
                    Disconnect();
                }
            }

            TcpObject.Connect(IpAddress, Port);//attempt connect

            #region CodeContractPostConditions
            _codeContractDependency.Requires<InvalidOperationException>(TcpObject.Connected, "Failed to connect to device. Device: " + ComponentName);
            #endregion

        }

        /// <summary>
        /// disconnect from component
        /// </summary>
        public override void Disconnect()
        {

            #region CodeContractPreconditions
            _codeContractDependency.Requires<ObjectDisposedException>(!disposed, "Failed to disconnect. Object has already been disposed. Device: " + ComponentName);
            #endregion

            TcpObject?.Client?.Close();//if tcp obj exists, close underlying client, if it exists
            TcpObject?.Close();//close client if it exists

        }

        #endregion

        /// <summary>
        /// Read byte wise from network stream until any termination character found
        /// </summary>
        /// <returns>Read message</returns>
        protected virtual string Read()
        {


            #region CodeContractPreconditions
            _codeContractDependency.Requires<InvalidOperationException>(IsConnected(), "Failed to read. Device not connected. Device: " + ComponentName);
            _codeContractDependency.Requires<InvalidOperationException>(TerminationCharacters != null, "Failed to read. Device has no configured termination character. Device: " + ComponentName);
            _codeContractDependency.Requires<InvalidOperationException>(TerminationCharacters.Length > 0, "Failed to read. Device has no configured termination character. Device: " + ComponentName);
            _codeContractDependency.Requires<InvalidOperationException>(TerminationCharacters.Where(c => !string.IsNullOrEmpty(c)).Count() > 0, "Failed to read. Device has no configured termination character. Device: " + ComponentName);
            #endregion

            string readMessage = "Unknown";
            string message = string.Empty;

            byte[] buffer = new byte[1];
            int nBytesRead = 0;

            var memStream = new MemoryStream();

            //set timeouts
            TcpObject.ReceiveTimeout = ReadTimeout;
            TcpObject.SendTimeout = WriteTimeout;

            NetworkStream nStream = TcpObject.GetStream();

            //init timeout mechanism
            Stopwatch timeoutWatch = new Stopwatch();
            timeoutWatch.Start();

            try
            {
                bool terminationCharFound = false;

                //continue reading until message termination character found
                while (!terminationCharFound)// while termination char not found
                {
                    //if data is available
                    if (nStream.DataAvailable)
                    {
                        //read from network stream
                        nBytesRead = nStream.Read(buffer, 0, buffer.Length);
                        //write to memory stream
                        memStream.Write(buffer, 0, nBytesRead);
                    }

                    //timeout if too much time passed
                    _codeContractDependency.Requires<TimeoutException>(
                        timeoutWatch.ElapsedMilliseconds < TcpObject.ReceiveTimeout, 
                        "Failed while reading response from device. " +
                        "Too much time passed without a response from the device. " + 
                        "Increase timeout if necessary. Device: " + ComponentName);

                    //convert mem stream to string
                    string currentMessage = MessageEncoding.GetString(memStream.ToArray());
                    //iterate over termination characters
                    foreach (var character in TerminationCharacters)
                    {
                        //check all specified termination characters
                        terminationCharFound = currentMessage.Contains(character);
                        //break as soon as one found
                        if (terminationCharFound)
                            break;
                    }
                }
            }
            finally
            {
                memStream.Close();
            }

            readMessage = MessageEncoding.GetString(memStream.ToArray());
            return readMessage;


        }

        /// <summary>
        /// Write message to network stream using specified encoding
        /// </summary>
        /// <param name="message"></param>
        protected virtual void Write(string message)
        {
            #region CodeContractPreconditions
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), "Failed to write. text cannot not be empty. Device: " + ComponentName);
            _codeContractDependency.Requires<InvalidOperationException>(IsConnected(), "Failed to write. Device not connected. Device: " + ComponentName);
            _codeContractDependency.Requires<ArgumentNullException>(MessageEncoding != null, "Failed to write. Encoding is not configured. Device: " + ComponentName);
            #endregion

            byte[] messageBytes = MessageEncoding.GetBytes(message);
            TcpObject.GetStream().Write(messageBytes, 0, messageBytes.Length);

        }


        #region IDisposable

        /// <summary>
        /// Handles releasing resources before objects death 
        /// </summary>
        /// <param name="disposing">Determines whether being invoked from within Dispose or a finalizer</param>
        protected override void Dispose(bool disposing)
        {
            //only release resources if this object has not alread been disposed
            if (!disposed)
            {
                //release resources if invoked from within Dispose
                if (disposing)
                {
                    TcpObject?.Client?.Close();//if tcp obj exists, close underlying client, if it exists
                    TcpObject?.Close();//close client if it exists
                    TcpObject = null;//set to null
                }
            }

        }

        #endregion

    }

    /// <summary>
    /// Enum representing possible digital IO state
    /// </summary>
    public enum IO_State
    {
        /// <summary>
        /// Indeterminate state
        /// </summary>
        NONE = -1,

        /// <summary>
        ///High state 
        /// </summary>
        HIGH = 1,

        /// <summary>
        ///Low state 
        /// </summary>
        LOW = 0
    }


    /// <summary>
    /// Base implementation of IComponentActionAttribute used for marking component actions
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ComponentActionAttribute : Attribute, IComponentActionAttribute
    {
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="memberAlias"></param>
        /// <param name="memberDescription"></param>
        /// <param name="memberId"></param>
        /// <param name="isIndependent"></param>
        public ComponentActionAttribute(
            string memberAlias,
            string memberDescription,
            string memberId,
            bool isIndependent)
        {
            MemberAlias = memberAlias;
            MemberDescription = memberDescription;
            MemberID = memberId;
            IsIndependent = isIndependent;
        }

        /// <summary>
        /// Method name
        /// </summary>
        public string MemberName { get; set; }

        /// <summary>
        /// Method human readable alias
        /// </summary>
        public string MemberAlias { get; set; }

        /// <summary>
        /// Description of method
        /// </summary>
        public string MemberDescription { get; set; }

        /// <summary>
        /// Method ID
        /// </summary>
        public string MemberID { get; set; }

        /// <summary>
        /// Flag indicating if method can run independently
        /// </summary>
        public bool IsIndependent { get; set; }
    }

    /// <summary>
    /// Base implementation of IComponentStateAttribute used for marking properties as component state
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ComponentStateAttribute : Attribute, IComponentStateAttribute
    {
        /// <summary>
        /// default ctor
        /// </summary>
        public ComponentStateAttribute() { }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="memberName"></param>
        /// <param name="memberAlias"></param>
        /// <param name="memberDescription"></param>
        /// <param name="memberId"></param>
        public ComponentStateAttribute(
            string memberName,
            string memberAlias,
            string memberDescription,
            string memberId)
        {
            MemberName = memberName;
            MemberAlias = memberAlias;
            MemberDescription = memberDescription;
            MemberID = memberId;
        }

        /// <summary>
        /// Property name
        /// </summary>
        public string MemberName { get; set; }
        /// <summary>
        /// Human readable Property alias
        /// </summary>
        public string MemberAlias { get; set; }
        /// <summary>
        /// Property description
        /// </summary>
        public string MemberDescription { get; set; }
        /// <summary>
        /// Unique ID for Property
        /// </summary>
        public string MemberID { get; set; }
    }


    /// <summary>
    /// Base implementation of IComponentActionParameterAttribute used for marking action parameters
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class ComponentActionParameterAttribute : Attribute, IComponentActionParameterAttribute
    {

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="memberAlias"></param>
        /// <param name="memberDescription"></param>
        /// <param name="memberId"></param>
        public ComponentActionParameterAttribute(
            string memberAlias,
            string memberDescription,
            string memberId)
        {
            MemberAlias = memberAlias;
            MemberDescription = memberDescription;
            MemberID = memberId;
        }

        /// <summary>
        /// Parameter name
        /// </summary>
        public string MemberName { get; set; }
        /// <summary>
        /// Human readable parameter alias
        /// </summary>
        public string MemberAlias { get; set; }
        /// <summary>
        /// parameter description 
        /// </summary>
        public string MemberDescription { get; set; }
        /// <summary>
        /// Unique ID for parameter
        /// </summary>
        public string MemberID { get; set; }
    }

}
