using AdapterBaseClasses;
using CommonServiceInterfaces;
using ComponentInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace MDriveMotors
{

    [DataContract]
    public abstract class CartesianRobot : ComponentAdapter, ICartesianRobotAdapter
    {
        //for serialization
        protected CartesianRobot()
        {
            InitializeMembers();
        }

        public CartesianRobot(
            ICodeContractService _codeContractDependency,
            ITypeManipulator _typeManipulatorDependency)
            : base(_codeContractDependency)
        {

            string failPrefix = "Failed in ctor for Cartesian robot. ";

            //init motor dependencies
            MotorX = new NEMA_23_MDriveMotor(_codeContractDependency, _typeManipulatorDependency);
            MotorY = new NEMA_23_MDriveMotor(_codeContractDependency, _typeManipulatorDependency);
            MotorZ = new NEMA_17_MDriveMotor(_codeContractDependency, _typeManipulatorDependency);

            typeManipulatorDependency = _typeManipulatorDependency ?? throw new ArgumentNullException(nameof(_typeManipulatorDependency), failPrefix + "type manipulation dependency canno be null");

            InitializeMembers();

        }


        private void InitializeMembers()
        {

            ComponentName = "Cartesian Robot";
        }


        protected ITypeManipulator typeManipulatorDependency;


        [DataMember]
        [Display]
        public NEMA_23_MDriveMotor MotorX { get; set; }

        [DataMember]
        [Display]
        public NEMA_23_MDriveMotor MotorY { get; set; }

        [DataMember]
        [Display]
        public NEMA_17_MDriveMotor MotorZ { get; set; }

        [DataMember]
        [Display]
        public int ZAxisSafePoint { get; set; }


        #region ICartesianRobotAdapter

        public override void InjectServiceProvider(IServiceProvider servProv)
        {
            base.InjectServiceProvider(servProv);
            MotorX.InjectServiceProvider(servProv);
            MotorY.InjectServiceProvider(servProv);
            MotorZ.InjectServiceProvider(servProv);
            typeManipulatorDependency = (ITypeManipulator)servProv.GetService(typeof(ITypeManipulator));
        }


        [ComponentAction(
            memberAlias: "Move to Coordinates",
            memberDescription: "Move robot to specified coordinates",
            memberId: "_moveToCoords",
            isIndependent: false)]
        public void MoveToCoordinates(
            [ComponentActionParameter("X Coordinate", "The target X position to which the robot should move", "_coordX")]
            int x,
            [ComponentActionParameter("Y Coordinate", "The target Y position to which the robot should move", "_coordY")]
            int y,
            [ComponentActionParameter("Z Coordinate", "The target Z position to which the robot should move", "_coordZ")]
            int z)
        {
            //get encoder dead band for each motor to determine the required positioning accuracy
            int xEncoderDB = MotorX.GetEncoderDeadBand();
            int yEncoderDB = MotorY.GetEncoderDeadBand();
            int zEncoderDB = MotorZ.GetEncoderDeadBand();

            //get current positions
            int xPos = MotorX.GetPosition();
            int yPos = MotorY.GetPosition();
            int zPos = MotorZ.GetPosition();

            //if not already at requested location
            if (Math.Abs(xPos - x) > xEncoderDB ||
                Math.Abs(yPos - y) > yEncoderDB ||
                Math.Abs(zPos - z) > zEncoderDB)
            {

                if (Math.Abs(zPos - ZAxisSafePoint) > zEncoderDB)//if difference greater than dead band position accuracy
                    MotorZ.ServoActuate(ZAxisSafePoint);//move motor z to configured safe point synchronously

                //init list of Tasks
                List<Task> actuateTasks = new List<Task>();

                if (Math.Abs(xPos - x) > xEncoderDB)//if difference greater than dead band position accuracy
                    actuateTasks.Add(Task.Run(() => { MotorX.ServoActuate(x); }));//move motor x to configured safe point synchronously
                if (Math.Abs(yPos - y) > yEncoderDB)//if difference greater than dead band position accuracy
                    actuateTasks.Add(Task.Run(() => { MotorY.ServoActuate(y); }));//move motor y to configured safe point synchronously

                //await xy move
                Task.WaitAll(actuateTasks.ToArray());

                //get new z position
                zPos = MotorZ.GetPosition();

                if (Math.Abs(zPos - z) > zEncoderDB)//if difference greater than dead band position accuracy
                    MotorZ.ServoActuate(z);//move motor z to requested position

            }

        }


        public ICartesianCoordinates GetCurrentCoordinates()
        {
            //init coords
            CartesianCoordinates coords = new CartesianCoordinates();

            //init list of Tasks
            Task[] positionTasks = new Task[3];

            //Add get position tasks to collection
            positionTasks[0] = Task.Run(() => { coords.X_Axis = MotorX.GetPosition(); });
            positionTasks[1] = Task.Run(() => { coords.Y_Axis = MotorY.GetPosition(); });
            positionTasks[2] = Task.Run(() => { coords.Z_Axis = MotorZ.GetPosition(); });

            //wait for all
            Task.WaitAll(positionTasks);

            return coords;
        }

        [ComponentAction(
            memberAlias: "Home",
            memberDescription: "Home robot",
            memberId: "_home",
            isIndependent: false)]
        public void Home()
        {
            //complete z home before x and y home to avoid crashing Z
            MotorZ.Home();

            //init list of Tasks
            Task[] homeTasks = new Task[2];

            //Add home tasks to collection
            homeTasks[0] = Task.Run(() => { MotorX.Home(); });
            homeTasks[1] = Task.Run(() => { MotorY.Home(); });

            //wait for all
            Task.WaitAll(homeTasks);
        }

        [ComponentAction(
            memberAlias: "Initialize",
            memberDescription: "Initialize all axes",
            memberId: "_initialize",
            isIndependent: false)]
        public override void Initialize()
        {
            //complete z home before x and y home to avoid crashing Z
            MotorZ.Initialize();

            //init list of Tasks
            Task[] initTasks = new Task[2];

            //Add get home tasks to collection
            initTasks[0] = Task.Run(() => { MotorX.Initialize(); });
            initTasks[1] = Task.Run(() => { MotorY.Initialize(); });

            //wait for all
            Task.WaitAll(initTasks);

        }

        public override string GetError()
        {

            string error = string.Empty;
            //init list of Tasks
            Task<string>[] errorTasks = new Task<string>[3];

            //Add error tasks to collection
            errorTasks[0] = Task.Run(() => { return "Motor X Error:" + MotorX.GetError() + Environment.NewLine; });
            errorTasks[1] = Task.Run(() => { return "Motor Y Error:" + MotorY.GetError() + Environment.NewLine; });
            errorTasks[2] = Task.Run(() => { return "Motor Z Error:" + MotorZ.GetError() + Environment.NewLine; });

            //concat errors
            errorTasks
                .Select(async (t) =>
                {
                    error += await t;
                });

            return error;
        }

        [ComponentAction(
            memberAlias: "Stop",
            memberDescription: "Stop actions on all axes",
            memberId: "_stop",
            isIndependent: false)]
        public override void Stop()
        {
            //init list of Tasks
            Task[] stopTasks = new Task[3];

            //Add stop tasks to collection
            stopTasks[0] = Task.Run(() => { MotorX.Stop(); });
            stopTasks[1] = Task.Run(() => { MotorY.Stop(); });
            stopTasks[2] = Task.Run(() => { MotorZ.Stop(); });

            //wait for all
            Task.WaitAll(stopTasks);

        }

        [ComponentAction(
            memberAlias: "Pause",
            memberDescription: "Pause actions on all axes",
            memberId: "_pause",
            isIndependent: false)]
        public override void Pause()
        {
            //init list of Tasks
            Task[] pauseTasks = new Task[3];

            //Add pause tasks to collection
            pauseTasks[0] = Task.Run(() => { MotorX.Pause(); });
            pauseTasks[1] = Task.Run(() => { MotorY.Pause(); });
            pauseTasks[2] = Task.Run(() => { MotorZ.Pause(); });

            //wait for all
            Task.WaitAll(pauseTasks);
        }

        [ComponentAction(
            memberAlias: "Resume",
            memberDescription: "Resume actions on all axes",
            memberId: "_resume",
            isIndependent: false)]
        public override void Resume()
        {
            //init list of Tasks
            Task[] resumeTasks = new Task[3];

            //Add resume tasks to collection
            resumeTasks[0] = Task.Run(() => { MotorX.Resume(); });
            resumeTasks[1] = Task.Run(() => { MotorY.Resume(); });
            resumeTasks[2] = Task.Run(() => { MotorZ.Resume(); });

            //wait for all
            Task.WaitAll(resumeTasks);
        }

        [ComponentAction(
            memberAlias: "Soft Reset",
            memberDescription: "Reset robot in software",
            memberId: "_reset",
            isIndependent: false)]
        public override void Reset()
        {
            //init list of Tasks
            Task[] resumeTasks = new Task[3];

            //Add reset tasks to collection
            resumeTasks[0] = Task.Run(() => { MotorX.Reset(); });
            resumeTasks[1] = Task.Run(() => { MotorY.Reset(); });
            resumeTasks[2] = Task.Run(() => { MotorZ.Reset(); });

            //wait for all
            Task.WaitAll(resumeTasks);
        }

        [ComponentAction(
            memberAlias: "Shutdown",
            memberDescription: "Stop motion and disconnect from robot",
            memberId: "_shutdown",
            isIndependent: false)]
        public override void ShutDown()
        {
            //init list of Tasks
            Task[] shutdownTasks = new Task[3];

            //Add shutdown tasks to collection
            shutdownTasks[0] = Task.Run(() => { MotorX.ShutDown(); });
            shutdownTasks[1] = Task.Run(() => { MotorY.ShutDown(); });
            shutdownTasks[2] = Task.Run(() => { MotorZ.ShutDown(); });

            //wait for all
            Task.WaitAll(shutdownTasks);
        }


        public override void Connect()
        {
            //init list of Tasks
            Task[] connectTasks = new Task[3];

            //Add connect tasks to collection
            connectTasks[0] = Task.Run(() => { MotorX.Connect(); });
            connectTasks[1] = Task.Run(() => { MotorY.Connect(); });
            connectTasks[2] = Task.Run(() => { MotorZ.Connect(); });

            //wait for all
            Task.WaitAll(connectTasks);
        }
        public override void Disconnect()
        {
            //init list of Tasks
            Task[] disconnectTasks = new Task[3];

            //Add disconnect tasks to collection
            disconnectTasks[0] = Task.Run(() => { MotorX.Disconnect(); });
            disconnectTasks[1] = Task.Run(() => { MotorY.Disconnect(); });
            disconnectTasks[2] = Task.Run(() => { MotorZ.Disconnect(); });

            //wait for all
            Task.WaitAll(disconnectTasks);
        }
        public override bool IsConnected()
        {
            //init connected flag
            bool isConnected = false;
            //init list of Tasks
            Task<bool>[] isConnectedTasks = new Task<bool>[3];

            //Add is connected tasks to collection
            isConnectedTasks[0] = Task.Run(() => { return MotorX.IsConnected(); });
            isConnectedTasks[1] = Task.Run(() => { return MotorY.IsConnected(); });
            isConnectedTasks[2] = Task.Run(() => { return MotorZ.IsConnected(); });

            isConnectedTasks.Select(
                async (t) =>
                {
                    isConnected = await t;
                    if (!isConnected)
                        return;
                });

            return isConnected;
        }
        public override void CommitConfiguredState()
        {
            //init list of Tasks
            Task[] commitTasks = new Task[3];

            //Add commit tasks to collection
            commitTasks[0] = Task.Run(() => { MotorX.CommitConfiguredState(); });
            commitTasks[1] = Task.Run(() => { MotorY.CommitConfiguredState(); });
            commitTasks[2] = Task.Run(() => { MotorZ.CommitConfiguredState(); });

            //wait for all
            Task.WaitAll(commitTasks);
        }
        public override void ReadState()
        {
            //init list of Tasks
            Task[] readTasks = new Task[3];

            //Add commit tasks to collection
            readTasks[0] = Task.Run(() => { MotorX.ReadState(); });
            readTasks[1] = Task.Run(() => { MotorY.ReadState(); });
            readTasks[2] = Task.Run(() => { MotorZ.ReadState(); });

            //wait for all
            Task.WaitAll(readTasks);
        }


        public abstract void Dock();
        public abstract string GetCurrentTeachPoint();
        public abstract void MoveToTeachpoint(string teachPoint);


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
                    MotorX?.Dispose();
                    MotorY?.Dispose();
                    MotorZ?.Dispose();
                }
            }

        }



        #endregion


    }

    public class EndEffectorComparer : IEqualityComparer<ICartesianEndEffectorMetadata>
    {
        public EndEffectorComparer() { }

        public bool Equals(ICartesianEndEffectorMetadata x, ICartesianEndEffectorMetadata y)
        {
            bool isSame = x.Name.Equals(y.Name, StringComparison.Ordinal);
            return isSame;
        }

        public int GetHashCode(ICartesianEndEffectorMetadata obj)
        {
            return StringComparer.Ordinal.GetHashCode(obj.Name);
        }

    }

    public sealed class TubeTeachpointInfo
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public string RackTeachpointName { get; set; }
    }

    public sealed class TubeMetricsModel
    {
        public string RackBarcode { get; set; }
        public int? Row { get; set; }
        public int? Column { get; set; }
        public string TubeBarcode { get; set; }
        public string Temperature { get; set; }
        public string Ph { get; set; }
        public string PhStandardDeviation { get; set; }
        public DateTime Timestamp { get; set; }

    }

    [DataContract]
    public class SpinTubeCartesianRobot : CartesianRobot
    {


        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {
            LoadTeachPointsWrapper();
            LogOutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CartesianRobotLog");
            LogOutputPath = Path.Combine(LogOutputDirectory, "ProcessLog_" + DateTime.Now.ToString("yyyy.MM.dd") + ".csv");

            if (!Directory.Exists(LogOutputDirectory))
                Directory.CreateDirectory(LogOutputDirectory);
        }

        private void LoadTeachPointsWrapper()
        {
            //init out vars
            IDictionary<string, ICartesianCoordinates> teachPoints;
            IDictionary<string, TubeTeachpointInfo> teachpointInfo;

            LoadTeachpointsAndTubeMappingFromFile(out teachPoints, out teachpointInfo);

            //if teachpoints were loaded
            if (teachPoints != null && teachPoints.Count > 0)
                Teachpoints = teachPoints;

            //if tp info was loaded
            if (teachpointInfo != null && teachpointInfo.Count > 0)
                TubeTeachPointInfo = teachpointInfo;//assign prop to loaded vals
        }

        //for serialization
        protected SpinTubeCartesianRobot() : base()
        {
            InitializeMembers();
        }

        public SpinTubeCartesianRobot(
            ICodeContractService _codeContractDependency,
            ITypeManipulator _typeManipulatorDependency,
            IComponentManager<IComponentAdapter, IComponentConstructionData> _componentManagerDependency)
            : base(_codeContractDependency, _typeManipulatorDependency)
        {
            InitializeMembers();

            componentManagerDependency = _componentManagerDependency;
        }


        protected IComponentManager<IComponentAdapter, IComponentConstructionData> componentManagerDependency;


        private void InitializeMembers()
        {
            SensePh = new CartesianEndEffectorMetaData();
            ReadBarcode = new CartesianEndEffectorMetaData();
            SetCompensationTemperature = new CartesianEndEffectorMetaData();
            DockingCoordinates = new CartesianCoordinates();
            Teachpoints = new Dictionary<string, ICartesianCoordinates>();
            TubeTeachPointInfo = new Dictionary<string, TubeTeachpointInfo>();
            ComponentName = "Spin Tube Cartesian Robot";

            LogOutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CartesianRobotLog");
            LogOutputPath = Path.Combine(LogOutputDirectory, "ProcessLog_" + DateTime.Now.ToString(DateFormatString) + ".csv");

            if (!Directory.Exists(LogOutputDirectory))
                Directory.CreateDirectory(LogOutputDirectory);

            LoadTeachPointsWrapper();
        }

        #region FIELDS

        protected const int RequiredTeachPointFileColumnsCount = 5;
        protected const string RackTypeName = "RACK";
        protected const string TubeTypeName = "TUBE";
        protected const char TeachPointFileDelimiter = ',';
        protected const bool HasHeader = true;
        protected const string DateFormatString = "yyyy.MM.dd";
        protected string LogOutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CartesianRobotLog");
        protected string LogOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CartesianRobotLog", "ProcessLog_" + DateTime.Now.Date.ToString("yyyy.MM.dd") + ".csv");

        #endregion


        #region PROPERTIES


        [DataMember]
        [Display]
        public string TeachpointFilePath { get; set; }

        [Display]
        [UIHint("Teachpoints")]
        public IDictionary<string, ICartesianCoordinates> Teachpoints { get; private set; }

        //[Display]
        //[UIHint("IntToStringDictionary")]
        //public IDictionary<int, string> TubeTeachPointMapping { get; set; }

        public IDictionary<string, TubeTeachpointInfo> TubeTeachPointInfo { get; set; }

        [DataMember]
        [Display]
        public ICartesianCoordinates DockingCoordinates { get; set; }

        [DataMember]
        [Display]
        [UIHint("EndEffector")]
        [DisplayName("Cartesian Action: pH")]
        public CartesianEndEffectorMetaData SensePh { get; set; }

        [DataMember]
        [Display]
        [UIHint("EndEffector")]
        [DisplayName("Cartesian Action: Read Temperature")]
        public CartesianEndEffectorMetaData SetCompensationTemperature { get; set; }

        [DataMember]
        [Display]
        [UIHint("EndEffector")]
        [DisplayName("Cartesian Action: Read Barcode")]
        public CartesianEndEffectorMetaData ReadBarcode { get; set; }


        #endregion


        #region ENCAPSULATION_METHODS


        protected string BuildOutputLineText(TubeMetricsModel data, string delimiter = ",")
        {

            //ensure data not null
            _codeContractDependency.Requires<ArgumentNullException>(
                data != null,
                "Failed to write data to output file. Input data cannot be null. Device: " +
                ComponentName);

            //ensure delimiter not empty or null
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(delimiter),
                "Failed to write data to output file. Delimiter cannot be null. Device: " +
                ComponentName);

            //build array of strings, handle null cases
            var outputLine = new string[]
            {
                data.RackBarcode ?? string.Empty,
                data.Row?.ToString() ?? string.Empty,
                data.Column?.ToString() ?? string.Empty,
                data.TubeBarcode ?? string.Empty,
                data.Temperature ?? string.Empty,
                data.Ph ?? string.Empty,
                data.PhStandardDeviation ?? string.Empty,
                data.Timestamp.ToString(DateFormatString) ?? string.Empty
            };

            //join the strings with sepcified delimiter
            string outputText = string.Join(delimiter, outputLine) + Environment.NewLine;

            return outputText;
        }

        protected void WriteOutputObjectToFile(TubeMetricsModel data, string outputPath = "", string delimiter = ",")
        {
            //construct output path if not specified
            if (string.IsNullOrEmpty(outputPath))
                outputPath = Path.Combine(LogOutputDirectory, "ProcessOutput_" + DateTime.Now.ToString(DateFormatString) + ".csv");

            //build outputline
            string outputText = BuildOutputLineText(data, delimiter);

            try
            {
                //write to output and log file
                File.AppendAllText(outputPath, outputText);

            }
            catch (Exception e)
            {
                throw new IOException("Failed to write to file. Specified Path: " + outputPath, e);
            }

            //only write to log if different path
            if (!outputPath.Equals(LogOutputPath, StringComparison.Ordinal))
                File.AppendAllText(LogOutputPath, outputText);
        }

        protected void WriteOutputObjectsToFile(List<TubeMetricsModel> data, string outputPath = "", string delimiter = ",")
        {
            //ensure data not null
            _codeContractDependency.Requires<ArgumentNullException>(
                data != null && data.Count > 0,
                "Failed to write data to output file. Input data cannot be null. Device: " +
                ComponentName);

            //construct output path if not specified
            if (string.IsNullOrEmpty(outputPath))
                outputPath = Path.Combine(LogOutputDirectory, "ProcessOutput_" + DateTime.Now.ToString(DateFormatString) + ".csv");

            //init output text
            string outputText = string.Empty;
            //iterate over data items
            foreach (var item in data)
            {
                //build output text
                outputText += BuildOutputLineText(item, delimiter);
            }

            //write to output and log file
            File.AppendAllText(outputPath, outputText);
            File.AppendAllText(LogOutputPath, outputText);

        }


        protected void LoadTeachpointsAndTubeMappingFromFile(
            out IDictionary<string, ICartesianCoordinates> teachPoints,
            out IDictionary<string, TubeTeachpointInfo> teachPointInfo)
        {
            teachPoints = null;
            teachPointInfo = null;

            //if tp file exists
            if (!string.IsNullOrEmpty(TeachpointFilePath))
            {
                var tempTeachpointCollection = new Dictionary<string, ICartesianCoordinates>();
                var tempTeachpointInfo = new Dictionary<string, TubeTeachpointInfo>();

                //open stream to tp file
                using (var fs = File.OpenRead(TeachpointFilePath))
                using (var reader = new StreamReader(fs))
                {
                    //if not at EOF and has header
                    if (!reader.EndOfStream && HasHeader)
                    {
                        //move past header
                        var header = reader.ReadLine();
                    }

                    //continue reading lines while not at the EOF
                    while (!reader.EndOfStream)
                    {
                        //read next line
                        var line = reader.ReadLine();
                        //split by delimiter, removing any empty
                        var values = line.Split(new char[] { TeachPointFileDelimiter }, StringSplitOptions.RemoveEmptyEntries);
                        //trim vals
                        var trimmedVals = values.Select(v => v.Trim()).ToArray();
                        //count values in line
                        int valCount = trimmedVals.Count();

                        //ensure valid number of columns
                        if (valCount < RequiredTeachPointFileColumnsCount)
                            throw new ArgumentOutOfRangeException(
                            "Failed to load in teachpoints file. Expected at least: " +
                            RequiredTeachPointFileColumnsCount +
                            " columns. Device: " +
                            ComponentName);

                        //ensure non empty tp name
                        if (string.IsNullOrEmpty(trimmedVals[0]))
                            throw new ArgumentException(
                                "Failed to load in teachpoints file. Teachpoint name cannot be empty. Device: " +
                                ComponentName);

                        //get name
                        string teachPointName = trimmedVals[0];

                        //declare cart coords
                        int x, y, z;
                        bool castedX, castedY, castedZ;

                        castedX = int.TryParse(trimmedVals[2], out x);
                        castedY = int.TryParse(trimmedVals[3], out y);
                        castedZ = int.TryParse(trimmedVals[4], out z);

                        //if didnt cast x
                        if (!castedX)
                            throw new InvalidCastException(
                                "Failed to load in teachpoints file. Failed to cast input coordinates X: " +
                                trimmedVals[2] +
                                ".Device: " +
                                ComponentName);

                        //if didnt cast y
                        if (!castedY)
                            throw new InvalidCastException(
                            "Failed to load in teachpoints file. Failed to cast input coordinates Y: " +
                            trimmedVals[3] +
                            ".Device: " +
                            ComponentName);

                        //if didnt cast z
                        if (!castedZ)
                            throw new InvalidCastException(
                            "Failed to load in teachpoints file. Failed to cast input coordinates Z: " +
                            trimmedVals[4] +
                            ".Device: " +
                            ComponentName);

                        //build cart coords obj
                        var tempTeachPoint = new CartesianCoordinates()
                        {
                            X_Axis = x,
                            Y_Axis = y,
                            Z_Axis = z
                        };

                        //add to running collection of tps
                        tempTeachpointCollection.Add(teachPointName, tempTeachPoint);


                        //get type
                        string teachPointType = trimmedVals[1];

                        //if not a recognized type
                        if (!teachPointType.Equals(RackTypeName, StringComparison.OrdinalIgnoreCase) && !teachPointType.Equals(TubeTypeName, StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("Failed to load teachpoints. Teachpoint should be either type: " +
                                RackTypeName +
                                " or type: " +
                                TubeTypeName);

                        //if optional columns supplied
                        if (valCount == RequiredTeachPointFileColumnsCount + 3)
                        {

                            //init row, col var
                            int row;
                            int column;

                            bool convertedRow = int.TryParse(trimmedVals[5], out row);
                            bool convertedCol = int.TryParse(trimmedVals[6], out column);
                            string rackTeachPointName = trimmedVals[7];


                            if (!convertedRow)
                                throw new InvalidCastException(
                                    "Failed to load teachpoints file. Failed to cast tube row: " +
                                    trimmedVals[6] + ". Device: " + ComponentName);

                            if (!convertedCol)
                                throw new InvalidCastException(
                                "Failed to load teachpoints file. Failed to cast tube column: " +
                                trimmedVals[7] +
                                ". Device: " +
                                ComponentName);

                            //init new teachpoint info obj
                            var teachpointInfo = new TubeTeachpointInfo
                            {
                                Row = row,
                                Column = column,
                                RackTeachpointName = rackTeachPointName
                            };

                            //add it tp info collection
                            tempTeachpointInfo.Add(teachPointName, teachpointInfo);

                        }
                    }
                }

                //assign private var to new colleciton of teach points
                teachPoints = tempTeachpointCollection;
                //assign private var to new collection of tube coordinates
                teachPointInfo = tempTeachpointInfo;
            }
        }

        protected bool ValidateEndEffectorAction(CartesianEndEffectorMetaData action, Type requiredInterface)
        {
            bool isValid = false;

            //ensure all fields assigned
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(action.ComponentID), "Failed to set end effector for sensing pH. Component ID cannot be empty. Device: " + ComponentName);
            _codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(action.AdapterProfile), "Failed to set end effector for sensing pH. Profile name cannot be empty. Device: " + ComponentName);

            //fetch adapter
            IComponentAdapter adapter = (IComponentAdapter)componentManagerDependency.GetComponentProfile(action.ComponentID, action.AdapterProfile)?.ComponentAdapterProfile;
            //ensure adapter found
            _codeContractDependency.Requires<ArgumentNullException>(adapter != null, "Failed to set end effector for sensing pH. Component not found. Device: " + ComponentName);
            //ensure implements correct interface
            bool isValidType = requiredInterface.IsAssignableFrom(adapter.GetType());
            _codeContractDependency.Requires<InvalidCastException>(isValidType, "Failed to set end effector for sensing pH. Component does not implement required interface: " + requiredInterface + ". Device: " + ComponentName);

            //action valid
            isValid = true;

            return isValid;

        }

        //public int GetCurrentTubeIndex()
        //{
        //    //ensure teachpoint mapping exist
        //    codeContractDependency.Requires<ArgumentNullException>(
        //        TeachPointInfo != null && TeachPointInfo.Count > 0,
        //        "Failed to get current tube index. " +
        //        "Teachpoint mapping object cannot be null or empty. " +
        //        "Device: " + ComponentName);

        //    codeContractDependency.Requires<ArgumentNullException>(
        //        Teachpoints != null && Teachpoints.Count > 0,
        //        "Failed to get current tube index. " +
        //        "teachpoints object cannot null or empty. " +
        //        "Device: " + ComponentName);

        //    //get current coords
        //    ICartesianCoordinates currentCoords = GetCurrentCoordinates();

        //    //try to find tp key
        //    string teachPointKey = Teachpoints
        //        .Where(
        //            kvp =>
        //            kvp.Value.X_Axis == currentCoords.X_Axis
        //            && kvp.Value.Y_Axis == currentCoords.Y_Axis
        //            && kvp.Value.Z_Axis == currentCoords.Z_Axis)?
        //        .Select(kvp => kvp.Key)?
        //        .SingleOrDefault();

        //    //ensure tp found
        //    codeContractDependency.Requires<KeyNotFoundException>(
        //        teachPointKey != null,
        //        "Failed to get current tube index. " +
        //        "Did not locate a configured teachpoint that matches the current axes positions. " +
        //        "Device: " + ComponentName);

        //    //try to find tube index from tp key
        //    int? tubeIndex =
        //        TeachPointInfo
        //        .SingleOrDefault(kvp => kvp.Value.Equals(teachPointKey, StringComparison.Ordinal))
        //        .Key;

        //    //ensure tube index found
        //    codeContractDependency.Requires<KeyNotFoundException>(
        //        tubeIndex != null,
        //        "Failed to get current tube index. " +
        //        "Found a teachpoint that matches the current axes positions, but it has no tube index associated with it. " +
        //        "Device: " + ComponentName);

        //    return (int)tubeIndex;

        //}

        protected string OperateOnTarget(
            CartesianEndEffectorMetaData endEffector,
            ICartesianCoordinates initialCoords,
            bool shouldOffset,
            bool throwOnOperationFailure = true)
        {

            string output = string.Empty;

            int xOffsetAbsolute = initialCoords.X_Axis;
            int yOffsetAbsolute = initialCoords.Y_Axis;
            int zOffsetAbsolute = initialCoords.Z_Axis;

            if (shouldOffset)
            {
                //calculate absolute position for offset
                xOffsetAbsolute = xOffsetAbsolute + endEffector.XOffset;
                yOffsetAbsolute = yOffsetAbsolute + endEffector.YOffset;
                zOffsetAbsolute = zOffsetAbsolute + endEffector.ZOffset;
            }
            

            //move to offset for end effector
            MoveToCoordinates(xOffsetAbsolute, yOffsetAbsolute, zOffsetAbsolute);
            //get comp profile
            var endEffectorProf = componentManagerDependency.GetComponentProfile(endEffector.ComponentID, endEffector.AdapterProfile);
            //gte ctor data
            var ctorData = (IComponentConstructionData)endEffectorProf?.ConstructionData;
            //invoke end effector actions and get result
            var endEffectorAdapter = (ISensorAdapter)endEffectorProf?.ComponentAdapterProfile;
            //ensure component can be loaded
            _codeContractDependency.Requires<ArgumentNullException>(endEffectorAdapter != null, "Failed to read barcode. Component failed to load. Component: " + ComponentName);

            try
            {
                //connect
                endEffectorAdapter.Connect();
                //scan
                output = endEffectorAdapter.Scan();
                //persist state
                componentManagerDependency.UpdateComponentProfile(endEffectorAdapter, ctorData);
                //disconnect
                endEffectorAdapter.Disconnect();

            }
            catch (Exception)
            {
                //throw if specified
                if (throwOnOperationFailure)
                    throw;

                //swallow exception and write failure to report
                output = "Failed";

            }
            finally
            {
                //always dispose
                endEffectorAdapter.Dispose();
            }


            return output;

        }

        protected void ValidateSenseTubeInputAndGetData(
            int tubeRow,
            int tubeCol,
            string rackTeachpointName,
            out ICartesianCoordinates coords,
            out TubeTeachpointInfo teachPointInfo)
        {

            //ensure rack tp non empty
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(rackTeachpointName), "Failed to process tube. Rack teachpoint cannot be empty. Device: " + ComponentName);

            //ensure tube row greater than 0
            _codeContractDependency.Requires<ArgumentException>(
                tubeRow > 0, "Failed to process tube. Row must be greater than 0. Device: " + ComponentName);

            //ensure tube col greater than 0
            _codeContractDependency.Requires<ArgumentException>(
                tubeCol > 0, "Failed to process tube. Column must be greater than 0. Device: " + ComponentName);

            //ensure tp obj non null
            _codeContractDependency.Requires<ArgumentNullException>(
                Teachpoints != null,
                "Failed to scan tube barcode. Teachpoints object cannot be null. " +
                "Device: " + ComponentName);
            //ensure tp info non null
            _codeContractDependency.Requires<NullReferenceException>(
                TubeTeachPointInfo != null,
                "Failed to scan tube barcode. Teachpoints info object cannot be null. " +
                "Device: " + ComponentName);

            //try to get teachpoint info based on row, col, and rack name
            var currentInfo =
                TubeTeachPointInfo
                .Where(i => i.Value.Row == tubeRow && i.Value.Column == tubeCol && i.Value.RackTeachpointName.Equals(rackTeachpointName, StringComparison.Ordinal));

            //ensure non null
            _codeContractDependency.Requires<NullReferenceException>(
               currentInfo != null,
               "Failed to scan tube barcode. Unable to find teachpoint corresponding to specified row, column , and rack. " +
               "Device: " + ComponentName);

            //ensure only one
            _codeContractDependency.Requires<ArgumentOutOfRangeException>(
               currentInfo.Count() == 1,
               "Failed to scan tube barcode. Found more than one teachpoint associated with the specified row, col, and rack" +
               "Device: " +
               ComponentName);

            //ensure specified rack tp exists
            _codeContractDependency.Requires<NullReferenceException>(
                Teachpoints.ContainsKey(rackTeachpointName),
                "Failed to scan tube barcode. No teachpoint associated with the specified rack teachpoint: " +
                rackTeachpointName +
                ". " +
                "Device: " + ComponentName);

            //get single info
            var singleInfo = currentInfo.Single();



            //declare coordinates
            ICartesianCoordinates _coords;
            //ensure coords found
            _codeContractDependency.Requires<NullReferenceException>(
                Teachpoints.TryGetValue(singleInfo.Key, out _coords),
                "Failed to scan tube barcode. unable to find coordinates for teachpoint: " +
                singleInfo.Key +
                "Device: " +
                ComponentName);

            //set out vars
            teachPointInfo = singleInfo.Value;
            coords = _coords;
        }


        [ComponentAction(
            memberAlias: "Read Tube pH",
            memberDescription: "Move to tube and read its pH",
            memberId: "_readTubePh",
            isIndependent: false)]
        public string ReadTubePh(
            [ComponentActionParameter("Tube Row", "The rack's 1-based row index where the tube is located", "_tubeRow")]
            int tubeRow,
            [ComponentActionParameter("Tube Column", "The rack's 1-based column index where the tube is located", "_tubeColumn")]
            int tubeCol,
            [ComponentActionParameter("Rack Teachpoint Name", "The teachpoint name corresponding to the rack in which the tube is located", "_rackTeachpointName")]
            string rackTeachpointName,
            [ComponentActionParameter("Output Path", "The path where the proccessing report should be written. Will be written to default log location if none supplied", "_outputPath")]
            string outputFilePath = "",
            [ComponentActionParameter("Stop on sense failure", "Whether or not to stop if sensing fails on a tube", "_throwOnSenseFail")]
            bool throwOnSenseFailure = true,
            [ComponentActionParameter("Write to Output", "Whether or not to write to an output file", "_writeToOutput")]
            bool writeToOutput = true)
        {

            ICartesianCoordinates coords;
            TubeTeachpointInfo teachPointInfo;

            #region PRECONDITIONS

            //VALIDATE INPUT
            ValidateSenseTubeInputAndGetData(
                tubeRow: tubeRow,
                tubeCol: tubeCol,
                rackTeachpointName: rackTeachpointName,
                coords: out coords,
                teachPointInfo: out teachPointInfo);

            #endregion


            //get tube row col
            int row = teachPointInfo.Row;
            int col = teachPointInfo.Column;

            //if empty path or only writing to log file
            if (string.IsNullOrEmpty(outputFilePath) || !writeToOutput)
                outputFilePath = Path.Combine(LogOutputDirectory, "ProcessOutput_" + DateTime.Now.ToString(DateFormatString) + ".csv");

            //init output model
            var reportVals = new TubeMetricsModel();

            //add to info
            reportVals.RackBarcode = string.Empty;

            //add row col info
            reportVals.Row = row;
            reportVals.Column = col;

            try
            {
                //read temp 
                string temp = OperateOnTarget(SetCompensationTemperature, coords, throwOnSenseFailure);

                double tempNumeric;
                //attempt parse
                bool isParsed = double.TryParse(temp, out tempNumeric);

                //if not parsed
                if (!isParsed)
                {
                    //if not throw on sensor failure
                    if (!throwOnSenseFailure)
                    {
                        //set default temp to 25
                        tempNumeric = 25;
                    }
                    else
                    {
                        //else throw
                        throw new InvalidCastException("Failed read tube pH tube. Failed to convert temp string to double. Device: " + ComponentName);
                    }
                }

                //add to reported vals
                reportVals.Temperature = tempNumeric.ToString();

                //set comp temp
                SetPhCompensationTemperature(tempNumeric);

                //read pH
                string ph = OperateOnTarget(
                    endEffector: SensePh, 
                    initialCoords: coords, 
                    shouldOffset: true,
                    throwOnOperationFailure: throwOnSenseFailure);

                //read pH
                reportVals.Ph = ph;
                //get standard dev
                reportVals.PhStandardDeviation = GetPhStandardDeviation().ToString();
                //get timestamp
                reportVals.Timestamp = DateTime.Now;

                Task[] tasks = new Task[2];

                //start thread pool task to move to safe z
                tasks[0] = Task.Run(() =>
                {
                    //move to safe z
                    MotorZ.ServoActuate(ZAxisSafePoint);
                });
                //start thread pool task to write to file
                tasks[1] = Task.Run(() =>
                {
                    //write vals to file
                    WriteOutputObjectToFile(reportVals, outputFilePath, ",");
                });

                //wait for all before returning
                Task.WaitAll(tasks);

                return ph;
            }
            catch (Exception)
            {
                //move to safe z
                MotorZ.ServoActuate(ZAxisSafePoint);
                throw;
            }

        }

        [ComponentAction(
            memberAlias: "Scan Tube Barcode",
            memberDescription: "Move to tube and scan its barcode",
            memberId: "_scanTubeBarcode",
            isIndependent: false)]
        public string ScanTubeBarcode(
            [ComponentActionParameter("Tube Row", "The rack's 1-based row index where the tube is located", "_tubeRow")]
            int tubeRow,
            [ComponentActionParameter("Tube Column", "The rack's 1-based column index where the tube is located", "_tubeColumn")]
            int tubeCol,
            [ComponentActionParameter("Rack Teachpoint Name", "The teachpoint name corresponding to the rack in which the tube is located", "_rackTeachpointName")]
            string rackTeachpointName,
            [ComponentActionParameter("Rack Barcode", "The barcode of the rack in which the tube exists. Used in output file", "_rackBarcode")]
            string rackBarcode = "",
            [ComponentActionParameter("Output Path", "The path where the proccessing report should be written. Will be written to default log location if none supplied", "_outputPath")]
            string outputFilePath = "",
            [ComponentActionParameter("Stop on sense failure", "Whether or not to stop if sensing fails on a tube", "_throwOnSenseFail")]
            bool throwOnScanFailure = true,
            [ComponentActionParameter("Scan Rack Barcode", "Whether or not to scan the rack barcode", "_scanRackBarcode")]
            bool scanRack = true,
            [ComponentActionParameter("Write to Output", "Whether or not to write to an output file", "_writeToOutput")]
            bool writeToOutput = true)
        {

            ICartesianCoordinates coords;
            TubeTeachpointInfo teachPointInfo;

            #region PRECONDITIONS

            //VALIDATE INPUT
            ValidateSenseTubeInputAndGetData(
                tubeRow: tubeRow,
                tubeCol: tubeCol,
                rackTeachpointName: rackTeachpointName,
                coords: out coords,
                teachPointInfo: out teachPointInfo);

            #endregion


            //get tube row col
            int row = teachPointInfo.Row;
            int col = teachPointInfo.Column;

            //if empty path or only writing to log file
            if (string.IsNullOrEmpty(outputFilePath) || !writeToOutput)
                outputFilePath = Path.Combine(LogOutputDirectory, "ProcessOutput_" + DateTime.Now.ToString(DateFormatString) + ".csv");

            //init output model
            var reportVals = new TubeMetricsModel();

            //init rack bc
            string rackBC;
            try
            {
                //if scanning rack
                if (scanRack)
                {
                    //scan rack if asked to
                    rackBC = ScanLocationBarcode(
                        teachPointName: rackTeachpointName,
                        outputFilePath: null,
                        throwOnScanFailure: throwOnScanFailure,
                        writeToOutput: false);

                    //if specified rack bc non empty
                    if (!string.IsNullOrEmpty(rackBarcode))
                    {
                        //throw if specified rack bc not equal to the scanned 
                        if (!rackBC.Equals(rackBarcode, StringComparison.InvariantCulture))
                            throw new ArgumentException(
                                "Failed to scan tube barcode. The specified rack barcode: " +
                                rackBarcode +
                                " does not match the scanned rack barcode: " +
                                rackBC);
                    }
                }
                //else use the supplied rack barcode
                else
                {
                    rackBC = rackBarcode;
                }

                //add to info
                reportVals.RackBarcode = rackBC;

                //add row col info
                reportVals.Row = row;
                reportVals.Column = col;

                //scan
                string tubeBarcode = OperateOnTarget(
                    endEffector: ReadBarcode,
                    initialCoords: coords, 
                    shouldOffset: true,
                    throwOnOperationFailure: throwOnScanFailure);
                //get timestamp
                reportVals.Timestamp = DateTime.Now;
                //set tube bc
                reportVals.TubeBarcode = tubeBarcode;

                Task[] tasks = new Task[2];

                //start thread pool task to move to safe z
                tasks[0] = Task.Run(() =>
                {
                    //move to safe z
                    MotorZ.ServoActuate(ZAxisSafePoint);
                });
                //start thread pool task to write to file
                tasks[1] = Task.Run(() =>
                {
                    //write vals to file
                    WriteOutputObjectToFile(reportVals, outputFilePath, ",");
                });

                //wait for all before returning
                Task.WaitAll(tasks);


                return tubeBarcode;
            }
            catch (Exception)
            {
                //move to safe z
                MotorZ.ServoActuate(ZAxisSafePoint);
                throw;
            }


        }

        [ComponentAction(
            memberAlias: "Scan Location Barcode",
            memberDescription: "Move to location and scan its barcode",
            memberId: "_scanLocationBarcode",
            isIndependent: false)]
        public string ScanLocationBarcode(
            [ComponentActionParameter("Teach point key", "The name of the teachpoint you would like to move to before scanning barcode", "_teachpointName")]
            string teachPointName,
            [ComponentActionParameter("Output Path", "The path where the proccessing report should be written. Will be written to default log location if none supplied", "_outputPath")]
            string outputFilePath = "",
            [ComponentActionParameter("Stop on sense failure", "Whether or not to stop if scan fails", "_throwOnScanFail")]
            bool throwOnScanFailure = true,
            [ComponentActionParameter("Write to Output", "Whether or not to write to an output file", "_writeToOutput")]
            bool writeToOutput = true)
        {

            #region PRECONDITIONS

            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(teachPointName),
                "Failed to scan location. Teachpoint cannot be null. Device: " +
                ComponentName);

            _codeContractDependency.Requires<ArgumentNullException>(
                Teachpoints != null,
                "Failed to scan location. No teachpoints registered. Device: " +
                ComponentName);

            _codeContractDependency.Requires<ArgumentNullException>(
                Teachpoints.ContainsKey(teachPointName),
                "Failed to scan location. Teachpoint: " +
                teachPointName +
                " not found in teachpoint file. Device: " +
                ComponentName);

            #endregion


            if (string.IsNullOrEmpty(outputFilePath) || !writeToOutput)
                outputFilePath = Path.Combine(LogOutputDirectory, "ProcessOutput_" + DateTime.Now.ToString(DateFormatString) + ".csv");

            //get coords for current teachpoint key
            ICartesianCoordinates coords = Teachpoints[teachPointName];

            //scan
            string output = OperateOnTarget(
                endEffector: ReadBarcode, 
                initialCoords: coords, 
                shouldOffset: false,
                throwOnOperationFailure: throwOnScanFailure);

            var reportedVals = new TubeMetricsModel() { RackBarcode = output, Timestamp = DateTime.Now };

            //write to output if asked to
            if (writeToOutput)
                WriteOutputObjectToFile(reportedVals, outputFilePath, ",");

            return output;
        }

        protected void SetPhCompensationTemperature(double temperatureInCelsius)
        {
            //get comp profile
            var phProfile = componentManagerDependency.GetComponentProfile(SensePh.ComponentID, SensePh.AdapterProfile);
            //gte ctor data
            var ctorData = (IComponentConstructionData)phProfile?.ConstructionData;
            //invoke end effector actions and get result
            var phAdapter = (IpHSensorAdapter)phProfile?.ComponentAdapterProfile;

            //ensure component can be loaded
            _codeContractDependency.Requires<ArgumentNullException>(
                phAdapter != null,
                "Failed to set compensation temperature. Component failed to load. Component: " +
                ComponentName);

            try
            {
                //connect
                phAdapter.Connect();
                //set temp
                phAdapter.CompensationTemperature = temperatureInCelsius;
                //persist state
                componentManagerDependency.UpdateComponentProfile(phAdapter, ctorData);
                //disconnect
                phAdapter.Disconnect();

            }
            finally
            {
                //always dispose
                phAdapter.Dispose();
            }
        }

        protected double GetPhStandardDeviation()
        {
            //get comp profile
            var phProfile = componentManagerDependency.GetComponentProfile(SensePh.ComponentID, SensePh.AdapterProfile);
            //gte ctor data
            var ctorData = (IComponentConstructionData)phProfile?.ConstructionData;
            //get adapter 
            var phAdapter = (IpHSensorAdapter)phProfile?.ComponentAdapterProfile;

            //ensure component can be loaded
            _codeContractDependency.Requires<ArgumentNullException>(
                phAdapter != null,
                "Failed to get standard deviation. Component failed to load. Component: " +
                ComponentName);

            //init standard deviation
            double stdDev;

            try
            {
                stdDev = phAdapter.StandardDeviation;
            }
            finally
            {
                //always dispose
                phAdapter.Dispose();
            }

            return stdDev;
        }


        private TubeMetricsModel ProcessTubeInternal(int tubeRow,
            int tubeCol,
            string rackTeachpointName,
            string rackBarcode = "",
            string outputPath = "",
            bool throwOnSenseFailure = true,
            bool scanRack = true,
            bool writeToOutput = true)
        {
            #region PRECONDITIONS

            //ensure rack tp non empty
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(rackTeachpointName), "Failed to process tube. Rack teachpoint cannot be empty. Device: " + ComponentName);

            //ensure tube row greater than 0
            _codeContractDependency.Requires<ArgumentException>(
                tubeRow > 0, "Failed to process tube. Row must be greater than 0. Device: " + ComponentName);

            //ensure tube col greater than 0
            _codeContractDependency.Requires<ArgumentException>(
                tubeCol > 0, "Failed to process tube. Column must be greater than 0. Device: " + ComponentName);

            //ensure tp obj exists
            _codeContractDependency.Requires<ArgumentNullException>(
                Teachpoints != null,
                "Failed to process tube. Teachpoints object cannot be null. " +
                "Device: " + ComponentName);

            //ensure tp info exists
            _codeContractDependency.Requires<NullReferenceException>(
                TubeTeachPointInfo != null,
                "Failed to process tube. Teachpoints info object cannot be null. " +
                "Device: " + ComponentName);

            //try to get teachpoint info based on row, col, type and rack name
            var currentInfo =
                TubeTeachPointInfo
                .Where(i => i.Value.Row == tubeRow && i.Value.Column == tubeCol && i.Value.RackTeachpointName.Equals(rackTeachpointName, StringComparison.Ordinal));

            //ensure non null
            _codeContractDependency.Requires<NullReferenceException>(
               currentInfo != null,
               "Failed to process tube. Unable to find teachpoint corresponding to specified row, column , and rack. " +
               "Device: " + ComponentName);

            //ensure only one
            _codeContractDependency.Requires<ArgumentOutOfRangeException>(
               currentInfo.Count() == 1,
               "Failed to process tube. Found more than one teachpoint associated with the specified row, col, and rack" +
               "Device: " +
               ComponentName);

            //get single info
            var singleInfo = currentInfo.Single();

            //ensure specified rack tp exists
            _codeContractDependency.Requires<NullReferenceException>(
                Teachpoints.ContainsKey(rackTeachpointName),
                "Failed to process tube. No teachpoint associated with the specified rack teachpoint: " +
                rackTeachpointName +
                ". " +
                "Device: " + ComponentName);

            //declare coordinates
            ICartesianCoordinates coords;
            //ensure coords found
            _codeContractDependency.Requires<NullReferenceException>(
                Teachpoints.TryGetValue(singleInfo.Key, out coords),
                "Failed to process tube. unable to find coordinates for teachpoint: " +
                singleInfo.Key +
                "Device: " +
                ComponentName);

            #endregion

            //if log path not specified, construct default
            if (string.IsNullOrEmpty(outputPath) || !writeToOutput)
                outputPath = Path.Combine(LogOutputDirectory, "ProcessOutput_" + DateTime.Now.ToString(DateFormatString) + ".csv");

            //declare report vals obj
            var reportVals = new TubeMetricsModel();

            //init rack bc
            string rackBC;

            try
            {
                //if scanning rack
                if (scanRack)
                {
                    //scan rack if asked to
                    rackBC = ScanLocationBarcode(rackTeachpointName, null, throwOnSenseFailure);

                    //if specified rack bc non empty
                    if (!string.IsNullOrEmpty(rackBarcode))
                    {
                        //throw if specified rack bc not equal to the scanned 
                        if (!rackBC.Equals(rackBarcode, StringComparison.InvariantCulture))
                            throw new ArgumentException(
                                "Failed to scan tube barcode. The specified rack barcode: " +
                                rackBarcode +
                                " does not match the scanned rack barcode: " +
                                rackBC);
                    }
                }
                //else use the supplied rack barcode
                else
                {
                    rackBC = rackBarcode;
                }

                //add to reported vals
                reportVals.RackBarcode = rackBC;

                //get row col
                int row = singleInfo.Value.Row;
                int col = singleInfo.Value.Column;

                //add tube row
                reportVals.Row = row;
                //add tube col
                reportVals.Column = col;

                //scan bc 
                string bc = OperateOnTarget(
                    endEffector: ReadBarcode, 
                    initialCoords: coords,
                    shouldOffset: true,
                    throwOnOperationFailure: throwOnSenseFailure);
                //add timestamp
                reportVals.Timestamp = DateTime.Now;
                //add to reported vals
                reportVals.TubeBarcode = bc;
                //read temp 
                string temp = OperateOnTarget(
                    endEffector: SetCompensationTemperature,
                    initialCoords: coords, 
                    shouldOffset: true,
                    throwOnOperationFailure: throwOnSenseFailure);

                double tempNumeric;
                //attempt parse
                bool isParsed = double.TryParse(temp, out tempNumeric);

                //if not parsed
                if (!isParsed)
                {
                    //if not throw on sensor failure
                    if (!throwOnSenseFailure)
                    {
                        //set default temp to 25
                        tempNumeric = 25;
                    }
                    else
                    {
                        //else throw
                        throw new InvalidCastException("Failed process tube. Failed to convert temp string to double. Device: " + ComponentName);
                    }
                }

                //add to reported vals
                reportVals.Temperature = tempNumeric.ToString();

                //set comp temp
                SetPhCompensationTemperature(tempNumeric);

                //read pH
                string ph = OperateOnTarget(
                    endEffector: SensePh, 
                    initialCoords: coords, 
                    shouldOffset: true,
                    throwOnOperationFailure: throwOnSenseFailure);

                double stdDev = GetPhStandardDeviation();

                //add to reported vals
                reportVals.Ph = ph;
                reportVals.PhStandardDeviation = stdDev.ToString();

                Task[] tasks = new Task[2];

                //start thread pool task to move to safe z
                tasks[0] = Task.Run(() =>
                {
                    //move to safe z
                    MotorZ.ServoActuate(ZAxisSafePoint);
                });
                //start thread pool task to write to file
                tasks[1] = Task.Run(() =>
                {
                    //write vals to file
                    WriteOutputObjectToFile(reportVals, outputPath, ",");
                });

                //wait for all before returning
                Task.WaitAll(tasks);

                return reportVals;
            }
            catch (Exception)
            {
                //move to safe z
                MotorZ.ServoActuate(ZAxisSafePoint);
                throw;
            }

        }

        [ComponentAction(
            memberAlias: "Process Tube",
            memberDescription: "Move to tube and process it using all configured end effectors",
            memberId: "_processTube",
            isIndependent: false)]
        public void ProcessTube(
            [ComponentActionParameter("Tube Row", "The rack's 1-based row index where the tube is located", "_tubeRow")]
            int tubeRow,
            [ComponentActionParameter("Tube Column", "The rack's 1-based column index where the tube is located", "_tubeColumn")]
            int tubeCol,
            [ComponentActionParameter("Rack Teachpoint Name", "The teachpoint name corresponding to the rack in which the tube is located", "_rackTeachpointName")]
            string rackTeachpointName,
            [ComponentActionParameter("Rack Barcode", "The barcode of the rack in which the tube exists. Used in output file", "_rackBarcode")]
            string rackBarcode = "",
            [ComponentActionParameter("Output Path", "The path where the proccessing report should be written. Will be written to default log location if none supplied", "_outputPath")]
            string outputPath = "",
            [ComponentActionParameter("Stop on sense failure", "Whether or not to stop if sensing fails on a tube", "_throwOnSenseFail")]
            bool throwOnSenseFailure = true,
            [ComponentActionParameter("Scan Rack Barcode", "Whether or not to scan the rack barcode", "_scanRackBarcode")]
            bool scanRack = true,
            [ComponentActionParameter("Write to Output", "Whether or not to write to an output file", "_writeToOutput")]
            bool writeToOutput = true)
        {
            //call internal method
            ProcessTubeInternal(
                tubeRow: tubeRow,
                tubeCol: tubeCol,
                rackTeachpointName: rackTeachpointName,
                rackBarcode: rackBarcode,
                outputPath: outputPath,
                throwOnSenseFailure: throwOnSenseFailure,
                scanRack: scanRack,
                writeToOutput: writeToOutput);
        }

        [ComponentAction(
            memberAlias: "Process Rack",
            memberDescription: "Process all tubes registered with the specified rack with configured end effectors",
            memberId: "_processRack",
            isIndependent: false)]
        public void ProcessRack(
            [ComponentActionParameter("Rack Teach Point", "The name of the rack teachpoint corresponding to the rack that should be processed", "_rackTeachPoint")]
            string rackTeachPointName,
            [ComponentActionParameter("Output Path", "The path where the proccessing report should be written", "_outputPath")]
            string outputFilePath = "",
            [ComponentActionParameter("Stop on sense failure", "Whether or not to stop if sensing fails on a tube", "_throwOnSenseFail")]
            bool throwOnSenseFailure = true)
        {
            #region PRECONDITIONS

            //ensure rack tp non empty
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(rackTeachPointName),
                "Failed to process tubes. Rack teach point cannot be empty. Device: " +
                ComponentName);

            //ensure tp info exists
            _codeContractDependency.Requires<ArgumentNullException>(
                TubeTeachPointInfo != null && TubeTeachPointInfo.Count > 0,
                "Failed to process tubes. Tube teachpoint info obj cannot be null. Device: " +
                ComponentName);

            //try to get tube tp info
            var tubeInfoCollection =
                TubeTeachPointInfo
                .Where(i => i.Value.RackTeachpointName.Equals(rackTeachPointName, StringComparison.Ordinal));

            //ensure rack found in teachpoints info
            _codeContractDependency.Requires<ArgumentNullException>(
                tubeInfoCollection != null && tubeInfoCollection.Count() > 0,
                "Failed to process tubes. Could not find any tube teachpoints associated with rack: " +
                rackTeachPointName +
                ". Device: " +
                ComponentName);

            #endregion

            //scan rack bc but do not write to output file
            string rackBC = ScanLocationBarcode(
                rackTeachPointName,
                null,
                throwOnSenseFailure,
                writeToOutput: false);

            //iterate over tube tp info associated with rack tp
            foreach (var info in tubeInfoCollection)
            {
                //init temp var for dictionary value
                var tempInfoVal = info.Value;

                //call process tube but do not rescan the rack or write to output
                TubeMetricsModel tubeOutputInfo = ProcessTubeInternal(
                    tempInfoVal.Row,
                    tempInfoVal.Column,
                    tempInfoVal.RackTeachpointName,
                    string.Empty,
                    outputFilePath,
                    throwOnSenseFailure,
                    scanRack: false,
                    writeToOutput: false);

                //add rack info 
                tubeOutputInfo.RackBarcode = rackBC;

                //write it to file
                WriteOutputObjectToFile(tubeOutputInfo, outputFilePath, ",");
            }

        }

        [ComponentAction(
            memberAlias: "Scan Rack's Tube Barcodes",
            memberDescription: "Scan all tubes in a specified rack",
            memberId: "_scanRackTubeBarcodes",
            isIndependent: false)]
        public void ScanRackTubeBarcodes(
            [ComponentActionParameter("Rack Teachpoint Name", "The name of the rack teachpoint", "_rackTeachpointName")]
            string rackTeachPointName,
            [ComponentActionParameter("Output Path", "The path where the proccessing report should be written", "_outputPath")]
            string outputFilePath = "",
            [ComponentActionParameter("Stop on sense failure", "Whether or not to stop if sensing fails on a tube", "_throwOnSenseFail")]
            bool throwOnSenseFailure = true)
        {

            #region PRECONDITIONS

            //ensure rack tp non empty
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(rackTeachPointName),
                "Failed to scan tube in rack. Rack teach point cannot be empty. Device: " +
                ComponentName);

            //ensure tp info exists
            _codeContractDependency.Requires<ArgumentNullException>(
                TubeTeachPointInfo != null && TubeTeachPointInfo.Count > 0,
                "Failed to scan tube in rack. Tube teachpoint info obj cannot be null. Device: " +
                ComponentName);

            //try to get tube tp info
            var tubeInfoCollection =
                TubeTeachPointInfo
                .Where(i => i.Value.RackTeachpointName.Equals(rackTeachPointName, StringComparison.Ordinal));

            //ensure rack found in teachpoints info
            _codeContractDependency.Requires<ArgumentNullException>(
                tubeInfoCollection != null && tubeInfoCollection.Count() > 0,
                "Failed to scan tube in rack. Could not find any tube teachpoints associated with rack: " +
                rackTeachPointName +
                ". Device: " +
                ComponentName);


            #endregion

            //scan rack bc
            string rackBC = ScanLocationBarcode(rackTeachPointName, outputFilePath, throwOnSenseFailure, writeToOutput: false);

            //iterate over tube tp info associated with rack tp
            foreach (var info in tubeInfoCollection)
            {

                //init temp var for dictionary value
                var tempInfoVal = info.Value;

                //call process tube without writing to file
                string tempBC = ScanTubeBarcode(
                    tubeRow: tempInfoVal.Row,
                    tubeCol: tempInfoVal.Column,
                    rackTeachpointName: tempInfoVal.RackTeachpointName,
                    rackBarcode: "",
                    outputFilePath: null,
                    throwOnScanFailure: throwOnSenseFailure,
                    scanRack: false,
                    writeToOutput: false);

                //create output object
                var tempOutputData = new TubeMetricsModel()
                {
                    RackBarcode = rackBC,
                    Row = tempInfoVal.Row,
                    Column = tempInfoVal.Column,
                    TubeBarcode = tempBC,
                    Temperature = string.Empty,
                    Ph = string.Empty,
                    PhStandardDeviation = string.Empty,
                    Timestamp = DateTime.Now
                };

                //write it to file
                WriteOutputObjectToFile(tempOutputData, outputFilePath, ",");
            }

        }

        [ComponentAction(
            memberAlias: "Enable Motor Drives",
            memberDescription: "Enable transmission from motor to output shaft on all axes",
            memberId: "_enableDrives",
            isIndependent: false)]
        public void EnableDrives()
        {
            //init list of Tasks
            Task[] enableTasks = new Task[3];

            enableTasks[0] = Task.Run(() => { MotorX.EnableDrive(); });
            enableTasks[1] = Task.Run(() => { MotorY.EnableDrive(); });
            enableTasks[2] = Task.Run(() => { MotorZ.EnableDrive(); });

            Task.WaitAll(enableTasks);
        }

        [ComponentAction(
            memberAlias: "Disable Motor Drives",
            memberDescription: "Disable transmission from motor to output shaft on all axes",
            memberId: "_disableDrives",
            isIndependent: false)]
        public void DisableDrives()
        {
            //init list of Tasks
            Task[] disableTasks = new Task[3];

            disableTasks[0] = Task.Run(() => { MotorX.DisableDrive(); });
            disableTasks[1] = Task.Run(() => { MotorY.DisableDrive(); });
            disableTasks[2] = Task.Run(() => { MotorZ.DisableDrive(); });

            Task.WaitAll(disableTasks);
        }

        [ComponentAction(
            memberAlias: "Clear Errors",
            memberDescription: "Clears any motor errors",
            memberId: "_clearErrors",
            isIndependent: false)]
        public void ClearErrors()
        {
            //init list of Tasks
            Task[] clearTasks = new Task[3];

            clearTasks[0] = Task.Run(() => { MotorX.ClearErrorCode(); });
            clearTasks[1] = Task.Run(() => { MotorY.ClearErrorCode(); });
            clearTasks[2] = Task.Run(() => { MotorZ.ClearErrorCode(); });

            Task.WaitAll(clearTasks);
        }

        [ComponentAction(
            memberAlias: "Clear Stalls",
            memberDescription: "Clears any motor stalls",
            memberId: "_clearStalls",
            isIndependent: false)]
        public void ClearStalls()
        {
            //init list of Tasks
            Task[] clearTasks = new Task[3];

            clearTasks[0] = Task.Run(() => { MotorX.ClearStall(); });
            clearTasks[1] = Task.Run(() => { MotorY.ClearStall(); });
            clearTasks[2] = Task.Run(() => { MotorZ.ClearStall(); });

            Task.WaitAll(clearTasks);
        }

        //[ComponentAction(
        //    memberAlias: "Create New End Effector",
        //    memberDescription: "Create a new end effector for the cartesian robot. This might include a sensor such as a barcode reader.",
        //    memberId: "_createEndEffector",
        //    isIndependent: true)]
        //public void CreateEndEffectorMetaData(
        //    [ComponentActionParameter("End Effector ID", "The Id for the new end effector such as: Barcode Reader", "_key")]
        //    string key)
        //{

        //    codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key), "");
        //    codeContractDependency.Requires<NullReferenceException>(EndEffectorMetadata != null, "");
        //    codeContractDependency.Requires<NullReferenceException>(EndEffectorMetadata.Add(new CartesianEndEffectorMetaData() { Name = key }), "");

        //}

        //[ComponentAction(
        //    memberAlias: "Delete End Effector",
        //    memberDescription: "Remove from collection of end effectors for the cartesian robot",
        //    memberId: "_deleteEndEffector",
        //    isIndependent: true)]
        //public void DeleteEndEffectorMetaData(
        //    [ComponentActionParameter("End Effector ID", "The Id of the end effector you would like to delete", "_key")]
        //    string key)
        //{
        //    codeContractDependency.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key), "");
        //    codeContractDependency.Requires<NullReferenceException>(EndEffectorMetadata != null, "");
        //    var specifiedEndEffector = EndEffectorMetadata.FirstOrDefault(e => e.Name.Equals(key, StringComparison.Ordinal));
        //    codeContractDependency.Requires<ArgumentException>(specifiedEndEffector != null, "");
        //    codeContractDependency.Requires<ArgumentException>(EndEffectorMetadata.Remove(specifiedEndEffector), "");

        //}

        #endregion


        #region OverriddenCartesianBaseMembers

        public override void InjectServiceProvider(IServiceProvider servProv)
        {
            base.InjectServiceProvider(servProv);
            componentManagerDependency = (IComponentManager<IComponentAdapter, IComponentConstructionData>)servProv.GetService(typeof(IComponentManager<IComponentAdapter, IComponentConstructionData>));
        }

        [ComponentAction(
            memberAlias: "Dock Robot",
            memberDescription: "Move robot to docking coordinates",
            memberId: "_dock",
            isIndependent: false)]
        public override void Dock()
        {

            _codeContractDependency.Requires<NullReferenceException>(
                DockingCoordinates != null,
                "Failed to dock robot. Docking coordinates object cannot be null. Device: " +
                ComponentName);

            //move to docking coords
            MoveToCoordinates(DockingCoordinates.X_Axis, DockingCoordinates.Y_Axis, DockingCoordinates.Z_Axis);
        }

        [ComponentAction(
            memberAlias: "Move to Teach Point",
            memberDescription: "Move robot to specified teachpoint",
            memberId: "_moveToTeachpoint",
            isIndependent: false)]
        public override void MoveToTeachpoint(
            [ComponentActionParameter("Teach Point", "The teach point number as specified in the teach point file", "_teachPoint")]
            string teachPoint)
        {

            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(teachPoint),
                "Failed to move to teachpoint. Specified teachpoint cannot be null. Device: " +
                ComponentName);

            _codeContractDependency.Requires<ArgumentNullException>(
                Teachpoints != null && Teachpoints.Count > 0,
                "Failed to move to teachpoint. Teachpoints object cannot be null. Device: " +
                ComponentName);

            ICartesianCoordinates coords;

            _codeContractDependency.Requires<ArgumentNullException>(
                Teachpoints.TryGetValue(teachPoint, out coords),
                "Failed to move to teachpoint. Unable to locate teachpoint with name: " +
                teachPoint +
                ". Device: " +
                ComponentName);

            MoveToCoordinates(coords.X_Axis, coords.Y_Axis, coords.Z_Axis);
        }

        public override string GetCurrentTeachPoint()
        {
            //ensure teachpoints exist
            _codeContractDependency.Requires<ArgumentNullException>(
                Teachpoints != null && Teachpoints.Count > 0,
                "Failed to identify current teachpoint. Teachpoints object cannot be null. Device: " +
                ComponentName);

            //get current coords
            ICartesianCoordinates currentCoords = GetCurrentCoordinates();

            //try to find tp
            string teachPoint = Teachpoints
                .Where(
                    kvp =>
                    kvp.Value.X_Axis == currentCoords.X_Axis
                    && kvp.Value.Y_Axis == currentCoords.Y_Axis
                    && kvp.Value.Z_Axis == currentCoords.Z_Axis)?
                .Select(kvp => kvp.Key)?
                .SingleOrDefault();

            //ensure tp found
            _codeContractDependency.Requires<KeyNotFoundException>(
                !string.IsNullOrEmpty(teachPoint),
                "Failed to get current teachpoint. Failed to identify current teachpoint. Device: " +
                ComponentName);

            return teachPoint;

        }

        #endregion

        #region OVERRIDING_ADAPTER_BASE


        #endregion
    }



    /// <summary>
    /// A generic network enabled mdrive stepper motor
    /// </summary>
    [DataContract]
    public class MDriveMotor : TCP_IP_Adapter, IBidirectionalServoActuatorAdapter
    {



        //for serialization
        protected MDriveMotor() : base()
        {
            InitializeMembers();
        }

        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {
            MessageEncoding = Encoding.ASCII;
            TerminationCharacters = new string[] { MDriveSpecialCharacters.ErrorResponseCharacter, MDriveSpecialCharacters.SuccessResponseCharacter };
        }

        private void InitializeMembers()
        {
            #region objectInit

            RemoteActionTimeout = 20000;
            WriteTimeout = 10000;
            ReadTimeout = 10000;
            MessageEncoding = Encoding.ASCII;
            Port = 503;
            ComponentName = "Lexium MDrive Motor";
            TerminationCharacters = new string[] { MDriveSpecialCharacters.ErrorResponseCharacter, MDriveSpecialCharacters.SuccessResponseCharacter };


            //LocalAngularAcceleration = MinAngularAcceleration + 1;
            //LocalAngularDeceleration = MinAngularAcceleration + 1;
            //LocalMicroStepResolution = LexiumMicroStepResolution.PER_STEP_256;
            //LocalPercentMaxHoldingCurrent = AbsMinHoldingCurrentPercent + 1;
            //LocalPercentMaxRunningCurrent = AbsMinRunningCurrentPercent + 1;
            //LocalMaxAngularVelocity = 768000;
            //LocalInitialAngularVelocity = 1000;
            //LocalHasEncoder = false;
            //LocalEchoMode = EchoMode.ALL_DEFAULT;

            //LocalAnalogInput = new LexiumAnalogInput(Lexium_Input_Lines.ANALOG_INPUT, LexiumAnalogInputMode.VOLTAGE, LexiumAnalogInputLevel.LOW);
            //LocalDigitalInputs = new List<LexiumDigitalInput>() { new LexiumDigitalInput(), new LexiumDigitalInput(), new LexiumDigitalInput() };
            //LocalDigitalOutputs = new List<LexiumDigitalOutput>() { new LexiumDigitalOutput(), new LexiumDigitalOutput() };

            #endregion
        }

        public MDriveMotor(
            ICodeContractService codeContractDependency,
            ITypeManipulator typeManipulatorDependency)
            : base(codeContractDependency)
        {

            if (typeManipulatorDependency == null)
                throw new ArgumentNullException("");

            _typeManipulatorDependency = typeManipulatorDependency;

            InitializeMembers();
        }



        #region Dependencies

        [IgnoreDataMember]
        protected ITypeManipulator _typeManipulatorDependency;

        #endregion

        #region Fields

        //[DataMember]
        //[Display]
        //public const int MaxAngularAcceleration = 61035160;
        //[DataMember]
        //[Display]
        //public const int MinAngularAcceleration = 90;
        //[DataMember]
        //[Display]
        //public const int StepResToRPMConversionFactor = 10000;
        //[DataMember]
        //[Display]
        //public const int AbsMaxHoldingCurrentPercent = 100;
        //[DataMember]
        //[Display]
        //public const int AbsMinHoldingCurrentPercent = 0;
        //[DataMember]
        //[Display]
        //public const int AbsMaxRunningCurrentPercent = 100;
        //[DataMember]
        //[Display]
        //public const int AbsMinRunningCurrentPercent = 1;

        #endregion

        #region PROPERTIES


        #region TYPE_PROPERTIES

        ////Object properties. These are accessors to internal state/utility objects 
        //[DataMember]
        //[Display]
        //public LexiumHomingType HomingType { get; set; }


        #endregion

        #region MOTOR_PROPERTIES


        //public EchoMode ReadEchoMode()
        //{
        //    string commandText = ConstructReadMotorParameterText(LexiumCommandPrefixes.EchoMode);
        //    int response = WriteReadAndTryParseMotorParam<int>(commandText);
        //    _codeContractDependency.Requires<ArgumentOutOfRangeException>(Enum.IsDefined(typeof(EchoMode), response), "");
        //    LocalEchoMode = (EchoMode)response;
        //    return LocalEchoMode;
        //}
        //public void WriteEchoMode(EchoMode echoMode)
        //{
        //    string commandText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.EchoMode, ((int)echoMode).ToString());
        //    WriteRead(commandText, false);
        //    LocalEchoMode = echoMode;

        //}
        //[DataMember]
        //[Display]
        //public EchoMode LocalEchoMode { get; set; }

        public int ReadPosition()
        {
            string commandText = ConstructReadMotorParameterText(MDriveCommandPrefixes.Position);
            LocalPosition = WriteReadAndTryParseMotorParam<int>(commandText);
            return LocalPosition;
        }

        public void WritePosition(int position)
        {
            string commandText = ConstructWriteMotorParameterText(MDriveCommandPrefixes.MoveAbsolute, position.ToString());
            WriteRead(commandText, true);
            LocalPosition = position;
        }

        [DataMember]
        [ComponentState]
        [Display]
        public int LocalPosition { get; set; }

        //[DataMember]
        //[Display]
        //public bool LocalHasEncoder { get; set; }
        //[DataMember]
        //[Display]
        //public EncoderEnabled LocalIsEncoderEnabled { get; set; }

        //public EncoderEnabled ReadEncoderEnabled()
        //{
        //    //if has an encoder
        //    if (LocalHasEncoder)
        //    {
        //        string commandText = ConstructReadMotorParameterText(LexiumCommandPrefixes.EncoderEnabled);
        //        int response = WriteReadAndTryParseMotorParam<int>(commandText);

        //        _codeContractDependency.Requires<ArgumentOutOfRangeException>(Enum.IsDefined(typeof(EncoderEnabled), response), "");

        //        LocalIsEncoderEnabled = (EncoderEnabled)response;
        //    }
        //    //if not
        //    else
        //    {
        //        LocalIsEncoderEnabled = EncoderEnabled.NONE;
        //    }

        //    return LocalIsEncoderEnabled;
        //}

        //public void WriteEncoderEnabled(EncoderEnabled isEnabled)
        //{
        //    //if has encoder
        //    if (LocalHasEncoder)
        //    {
        //        string commandText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.EncoderEnabled, ((int)isEnabled).ToString());
        //        WriteRead(commandText, false);
        //        LocalIsEncoderEnabled = isEnabled;
        //    }
        //    //if not
        //    else
        //    {
        //        LocalIsEncoderEnabled = EncoderEnabled.NONE;
        //    }
        //}

        //[DataMember]
        //[Display]
        //public List<LexiumDigitalInput> LocalDigitalInputs
        //{
        //    get
        //    {
        //        if (localDigitalInputs == null)
        //        {
        //            localDigitalInputs = new List<LexiumDigitalInput>();
        //        }
        //        return localDigitalInputs;
        //    }
        //    set
        //    {
        //        localDigitalInputs = value;
        //    }
        //}
        //private List<LexiumDigitalInput> localDigitalInputs;

        //[DataMember]
        //[Display]
        //public List<LexiumDigitalOutput> LocalDigitalOutputs
        //{
        //    get
        //    {
        //        if (localDigitalOutputs == null)
        //        {
        //            localDigitalOutputs = new List<LexiumDigitalOutput>();
        //        }
        //        return localDigitalOutputs;
        //    }
        //    set
        //    {
        //        localDigitalOutputs = value;
        //    }
        //}
        //private List<LexiumDigitalOutput> localDigitalOutputs;

        //[DataMember]
        //[Display]
        //public LexiumAnalogInput LocalAnalogInput { get; set; }

        /// <summary>
        /// Read all input states from motor
        /// </summary>
        //public void ReadAndUpdateDigitalInputs()
        //{
        //    #region Preconditions
        //    _codeContractDependency.Requires<ArgumentNullException>(LocalDigitalInputs != null, "");//digital inputs list has to exist with non-zero length
        //    #endregion

        //    foreach (var input in LocalDigitalInputs)
        //    {
        //        int inputIndex = (int)input.LineIndex;//cast to int. invalid cast is guaranteed not to happen here
        //        string readInputText = LexiumCommandPrefixes.InputPrefix + inputIndex;//build command text
        //        string formattedReadInputText = ConstructReadMotorParameterText(readInputText);//format command text

        //        input.State = (IO_State)WriteReadAndTryParseMotorParam<int>(formattedReadInputText);//get input state. Invalid cast may happen here, but no way to handle it

        //    }
        //}

        ///// <summary>
        ///// Read analog input from motor
        ///// </summary>
        //public void ReadAndUpdateAnalogInput()
        //{
        //    #region Preconditions
        //    _codeContractDependency.Requires<ArgumentNullException>(LocalAnalogInput != null, "");
        //    #endregion

        //    string lineIndex = ((int)LocalAnalogInput.LineIndex).ToString();
        //    string commandText = ConstructReadMotorParameterText(LexiumCommandPrefixes.InputPrefix + lineIndex);
        //    LocalAnalogInput.AnalogValue = WriteReadAndTryParseMotorParam<double>(commandText);

        //}

        ///// <summary>
        ///// write all digital input settings to motor
        ///// </summary>
        //private void WriteInputConfigurations()
        //{
        //    #region Preconditions
        //    _codeContractDependency.Requires<ArgumentNullException>(LocalDigitalInputs != null || LocalDigitalInputs.Count > 0, "");//digital inputs list has to exist with non-zero length
        //    #endregion

        //    //commit the config state of inputs to motor
        //    foreach (LexiumDigitalInput input in LocalDigitalInputs)
        //    {
        //        string fullConfigText =
        //            ((int)input.LineIndex).ToString() +
        //            "," +
        //            ((int)input.InputType).ToString() +
        //            "," +
        //            ((int)input.ActiveState).ToString();

        //        string formattedConfigText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.SetupInputs, fullConfigText);
        //        WriteRead(formattedConfigText, false);
        //    }

        //}

        ///// <summary>
        ///// Write all digital output settings to motor 
        ///// </summary>
        //private void WriteOutputConfigurations()
        //{
        //    #region Preconditions
        //    _codeContractDependency.Requires<ArgumentNullException>(LocalDigitalOutputs != null || LocalDigitalOutputs.Count > 0, "");//digital inputs list has to exist with non-zero length
        //    #endregion

        //    //commit the config state of outputs to motor
        //    foreach (var output in LocalDigitalOutputs)
        //    {
        //        string fullConfigText =
        //            ((int)output.LineIndex).ToString() +
        //             "," +
        //            ((int)output.OutputType).ToString() +
        //            "," +
        //            ((int)output.ActiveState).ToString();

        //        string formattedConfigText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.SetupOutputs, fullConfigText);
        //        WriteRead(formattedConfigText, false);
        //    }
        //}


        //#region MOTOR_DYNAMICS

        ///// <summary>
        ///// Read the current acceleration from the motor in encoder counts/steps per second squared
        ///// </summary>
        ///// <returns>Motor acceleration in encoder counts/steps per second squared</returns>
        //public int ReadMotorAngularAcceleration()
        //{
        //    string commandText = ConstructReadMotorParameterText(LexiumCommandPrefixes.Acceleration);
        //    LocalAngularAcceleration = WriteReadAndTryParseMotorParam<int>(commandText);
        //    return LocalAngularAcceleration;
        //}
        ///// <summary>
        ///// Write the specified acceleration value to the motor in encoder counts/steps per second squared
        ///// </summary>
        ///// <param name="angularAcceleration">The new angular acceleration to write to the motor in encoder counts/steps per second squared</param>
        //public void WriteMotorAngularAcceleration(int angularAcceleration)
        //{
        //    #region CodeContractPreconditions 
        //    _codeContractDependency.Requires<ArgumentOutOfRangeException>(
        //         angularAcceleration > MinAngularAcceleration && angularAcceleration < MaxAngularAcceleration,
        //         "The specified acceleration: " +
        //         angularAcceleration +
        //         ". was out of range for device: " +
        //         ComponentName);

        //    #endregion

        //    string commandText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.Acceleration, angularAcceleration.ToString());
        //    WriteRead(commandText, false);
        //    LocalAngularAcceleration = angularAcceleration;
        //}

        ///// <summary>
        ///// Accessors for local value of motor acceleration in encoder counts/steps per second squared
        ///// </summary>
        //[DataMember]
        //[Display]
        //public int LocalAngularAcceleration
        //{
        //    get
        //    {
        //        return localAngularAcceleration;
        //    }
        //    set
        //    {
        //        #region CodeContractPreconditions
        //        _codeContractDependency.Requires<ArgumentOutOfRangeException>(
        //             value > MinAngularAcceleration && value < MaxAngularAcceleration,
        //             "The specified acceleration: " +
        //             value +
        //             ". was out of range " + MinAngularAcceleration + " : " + MaxAngularAcceleration + " for device: " +
        //             ComponentName);
        //        #endregion

        //        localAngularAcceleration = value;
        //    }
        //}
        //private int localAngularAcceleration;

        ///// <summary>
        ///// Read the current deceleration from the motor in encoder counts/steps per second squared
        ///// </summary>
        ///// <returns>Motor deceleration in encoder counts/steps per second squared</returns>
        //public int ReadMotorAngularDeceleration()
        //{

        //    string commandText = ConstructReadMotorParameterText(LexiumCommandPrefixes.Deceleration);
        //    LocalAngularDeceleration = WriteReadAndTryParseMotorParam<int>(commandText);
        //    return LocalAngularDeceleration;
        //}

        ///// <summary>
        ///// Write the specified deceleration value to the motor in encoder counts/steps per second squared
        ///// </summary>
        ///// <param name="angularDeceleration">The new angular deceleration to write to the motor in encoder counts/steps per second squared</param>
        //public void WriteMotorAngularDeceleration(int angularDeceleration)
        //{
        //    #region CodeContractPreconditions
        //    _codeContractDependency.Requires<ArgumentOutOfRangeException>(
        //         angularDeceleration > MinAngularAcceleration && angularDeceleration < MaxAngularAcceleration,
        //         "The specified deceleration: " +
        //         angularDeceleration +
        //         ". was out of range for device: " +
        //         ComponentName +
        //         ". Max/min deceleration is: " +
        //         MaxAngularAcceleration +
        //         " / " +
        //         MinAngularAcceleration);
        //    #endregion

        //    string commandText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.Deceleration, angularDeceleration.ToString());
        //    WriteRead(commandText, false);
        //    LocalAngularDeceleration = angularDeceleration;
        //}

        ///// <summary>
        ///// Accessors for local value of motor deceleration in encoder counts/steps per second squared
        ///// </summary>
        //[DataMember]
        //[Display]
        //public int LocalAngularDeceleration
        //{
        //    get
        //    {
        //        return localAngularAcceleration;
        //    }
        //    set
        //    {
        //        #region CodeContractPreconditions
        //        _codeContractDependency.Requires<ArgumentOutOfRangeException>(
        //             value > MinAngularAcceleration && value < MaxAngularAcceleration,
        //             "The specified deceleration: " +
        //             value +
        //             ". was out of range for device: " +
        //             ComponentName +
        //             ". Max/min deceleration is: " +
        //             MaxAngularAcceleration +
        //             " / " +
        //             MinAngularAcceleration);
        //        #endregion

        //        localAngularDeceleration = value;
        //    }
        //}
        //private int localAngularDeceleration;

        ///// <summary>
        ///// Read the current max velocity from the motor in encoder counts/steps per second squared
        ///// </summary>
        ///// <returns>Motor max velocity in encoder counts/steps per second</returns>
        //public int ReadMotorMaxAngularVelocity()
        //{

        //    string commandText = ConstructReadMotorParameterText(LexiumCommandPrefixes.MaximumVelocity);
        //    LocalMaxAngularVelocity = WriteReadAndTryParseMotorParam<int>(commandText);

        //    return LocalMaxAngularVelocity;
        //}

        ///// <summary>
        ///// Write the specified max velocity to motor in encoder counts/steps per second
        ///// </summary>
        ///// <param name="maxAngularVelocity">The new max angular velocity to write to the motor in encoder counts/steps per second</param>
        //public void WriteMotorMaxAngularVelocity(int maxAngularVelocity)
        //{
        //    #region CodeContractPreconditions
        //    int maxVelocity = (int)ReadMicroStepResolution() * StepResToRPMConversionFactor;
        //    _codeContractDependency.Requires<ArgumentOutOfRangeException>(
        //         maxAngularVelocity < maxVelocity || maxAngularVelocity > LocalInitialAngularVelocity,
        //         "The specified velocity: " +
        //         maxAngularVelocity +
        //         ". was out of range for device: " +
        //         ComponentName +
        //         ". Max/min velocity is: " +
        //         maxVelocity +
        //         " / " +
        //         LocalInitialAngularVelocity);
        //    #endregion

        //    string commandText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.MaximumVelocity, maxAngularVelocity.ToString());
        //    WriteRead(commandText, false);
        //    LocalMaxAngularVelocity = maxAngularVelocity;
        //}

        ///// <summary>
        ///// Accessors for local value of max velocity in encoder counts/steps per second
        ///// </summary>
        //[DataMember]
        //[Display]
        //public int LocalMaxAngularVelocity { get; set; }

        ///// <summary>
        ///// Read current motor initial velocity in encoder counts/steps per second
        ///// </summary>
        ///// <returns>Current motor initial velocity in encoder counts/steps per second</returns>
        //public int ReadInitialAngularVelocity()
        //{

        //    string commandText = ConstructReadMotorParameterText(LexiumCommandPrefixes.InitialVelocity);
        //    LocalInitialAngularVelocity = WriteReadAndTryParseMotorParam<int>(commandText);

        //    return LocalInitialAngularVelocity;
        //}

        ///// <summary>
        ///// Write specified motor initial velocity in encoder counts/steps per second
        ///// </summary>
        ///// <param name="initialAngularVelocity">The new initial angular velocity to write to the motor in encoder counts/steps per second</param>
        //public void WriteInitialAngularVelocity(int initialAngularVelocity)
        //{
        //    #region CodeContractPreconditions
        //    _codeContractDependency.Requires<ArgumentOutOfRangeException>(
        //         initialAngularVelocity < ReadMotorMaxAngularVelocity(),
        //         "The specified initial velocity: " +
        //         initialAngularVelocity +
        //         ". was out of range for device: " +
        //         ComponentName +
        //         ". Max initial velocity is: " +
        //         ReadMotorMaxAngularVelocity());
        //    #endregion

        //    string commandText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.InitialVelocity, initialAngularVelocity.ToString());
        //    WriteRead(commandText, false);
        //    LocalInitialAngularVelocity = initialAngularVelocity;
        //}

        ///// <summary>
        ///// Accessors for local value of initial angular velocity. Manufacturer default: 1000
        ///// </summary>
        //[DataMember]
        //[Display]
        //public int LocalInitialAngularVelocity { get; set; }

        ///// <summary>
        ///// Read holding current from motor in percent of max
        ///// </summary>
        ///// <returns>Percent of max holding current</returns>
        //public int ReadPercentMaxHoldingCurrent()
        //{

        //    string commandText = ConstructReadMotorParameterText(LexiumCommandPrefixes.HoldingCurrent);
        //    LocalPercentMaxHoldingCurrent = WriteReadAndTryParseMotorParam<int>(commandText);

        //    return LocalPercentMaxHoldingCurrent;
        //}

        ///// <summary>
        ///// Write specified holding current to motor in percent of max
        ///// </summary>
        ///// <param name="percentMaxHoldingCurrent">The new max holding current in percent of max</param>
        //public void WritePercentMaxHoldingCurrent(int percentMaxHoldingCurrent)
        //{
        //    #region CodeContractPreconditions
        //    _codeContractDependency.Requires<ArgumentOutOfRangeException>(
        //         percentMaxHoldingCurrent <= AbsMaxHoldingCurrentPercent || percentMaxHoldingCurrent >= AbsMinHoldingCurrentPercent,
        //         "The specified percent holding current: " +
        //         percentMaxHoldingCurrent +
        //         ". was out of range for device: " +
        //         ComponentName +
        //         ". Max/min percent holding current is: " +
        //         AbsMaxHoldingCurrentPercent +
        //         " / " +
        //         AbsMinHoldingCurrentPercent);

        //    #endregion

        //    string commandText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.HoldingCurrent, percentMaxHoldingCurrent.ToString());
        //    WriteRead(commandText, false);
        //    LocalPercentMaxHoldingCurrent = percentMaxHoldingCurrent;
        //}

        ///// <summary>
        ///// Accessors for local holding current in percent of max
        ///// </summary>
        //[DataMember]
        //[Display]
        //public int LocalPercentMaxHoldingCurrent
        //{
        //    get
        //    {
        //        return localPercentMaxHoldingCurrent;
        //    }
        //    set
        //    {
        //        #region CodeContractPreconditions
        //        _codeContractDependency.Requires<ArgumentOutOfRangeException>(
        //             value <= AbsMaxHoldingCurrentPercent || value >= AbsMinHoldingCurrentPercent,
        //             "The specified percent holding current: " +
        //             value +
        //             ". was out of range for device: " +
        //             ComponentName +
        //             ". Max/min percent holding current is: " +
        //             AbsMaxHoldingCurrentPercent +
        //             " / " +
        //             AbsMinHoldingCurrentPercent);
        //        #endregion

        //        localPercentMaxHoldingCurrent = value;
        //    }
        //}
        //private int localPercentMaxHoldingCurrent;

        ///// <summary>
        ///// Read running current from motor in percent of max
        ///// </summary>
        ///// <returns></returns>
        //public int ReadPercentMaxRunningCurrent()
        //{
        //    string commandText = ConstructReadMotorParameterText(LexiumCommandPrefixes.RunningCurrent);
        //    LocalPercentMaxRunningCurrent = WriteReadAndTryParseMotorParam<int>(commandText);

        //    return LocalPercentMaxRunningCurrent;
        //}

        ///// <summary>
        ///// Write specified running current from motor in percent of max
        ///// </summary>
        ///// <param name="percentMaxRunningCurrent">The new max running current in percent of max</param>
        //public void WritePercentMaxRunningCurrent(int percentMaxRunningCurrent)
        //{

        //    #region CodeContractPreconditions
        //    _codeContractDependency.Requires<ArgumentOutOfRangeException>(
        //         percentMaxRunningCurrent <= AbsMaxRunningCurrentPercent && percentMaxRunningCurrent >= AbsMinRunningCurrentPercent,
        //         "The specified percent running current: " +
        //         percentMaxRunningCurrent +
        //         ". was out of range for device: " +
        //         ComponentName +
        //         ". Max/min percent running current is: " +
        //         AbsMaxRunningCurrentPercent +
        //         " / " +
        //         AbsMinRunningCurrentPercent);

        //    #endregion


        //    string commandText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.RunningCurrent, percentMaxRunningCurrent.ToString());
        //    WriteRead(commandText, false);
        //    LocalPercentMaxRunningCurrent = percentMaxRunningCurrent;

        //}

        ///// <summary>
        ///// Accessors for local running current in percent of max
        ///// </summary>
        //[DataMember]
        //[Display]
        //public int LocalPercentMaxRunningCurrent
        //{
        //    get
        //    {
        //        return localPercentMaxRunningCurrent;
        //    }
        //    set
        //    {
        //        #region CodeContractPreconditions
        //        _codeContractDependency.Requires<ArgumentOutOfRangeException>(
        //         value <= AbsMaxRunningCurrentPercent && value >= AbsMinRunningCurrentPercent,
        //         "The specified percent running current: " +
        //         value +
        //         ". was out of range for device: " +
        //         ComponentName +
        //         ". Max/min percent running current is: " +
        //         AbsMaxRunningCurrentPercent +
        //         " / " +
        //         AbsMinRunningCurrentPercent);
        //        #endregion

        //        localPercentMaxRunningCurrent = value;
        //    }
        //}
        //private int localPercentMaxRunningCurrent;

        ///// <summary>
        ///// Read micro step resolution from motor in microsteps per step. There are 200 steps per rev
        ///// </summary>
        ///// <returns>Micro step resolution in microsteps per step</returns>
        //public LexiumMicroStepResolution ReadMicroStepResolution()
        //{

        //    string commandText = ConstructReadMotorParameterText(LexiumCommandPrefixes.MicrostepResolution);
        //    int resolution = WriteReadAndTryParseMotorParam<int>(commandText);
        //    _codeContractDependency.Requires<ArgumentOutOfRangeException>(Enum.IsDefined(typeof(LexiumMicroStepResolution), resolution), ComponentName + " not connected!");
        //    LocalMicroStepResolution = (LexiumMicroStepResolution)resolution;//cast int to enum

        //    return LocalMicroStepResolution;

        //}

        ///// <summary>
        ///// Write specified microsteps resolution in microsteps per step 
        ///// </summary>
        ///// <param name="microStepResolution">The new microstep resolution in microsteps per step</param>
        //public void WriteMicroStepResolution(LexiumMicroStepResolution microStepResolution)
        //{
        //    string commandText = ConstructWriteMotorParameterText(LexiumCommandPrefixes.MicrostepResolution, ((int)microStepResolution).ToString());
        //    WriteRead(commandText, false);
        //    LocalMicroStepResolution = microStepResolution;
        //}
        ///// <summary>
        ///// Accessors for local microstep resolution
        ///// </summary>
        //[DataMember]
        //[Display]
        //public LexiumMicroStepResolution LocalMicroStepResolution { get; set; }

        //#endregion


        /// <summary>
        /// Read error state from motor
        /// </summary>
        /// <returns>Error state of motor</returns>
        public bool ReadIsMotorErrored()
        {

            string errorText;
            string commandText = ConstructReadMotorParameterText(MDriveCommandPrefixes.Error);
            errorText = WriteRead(commandText, false);
            LocalIsMotorErrored = errorText != "0";

            return LocalIsMotorErrored;
        }

        [ComponentState]
        [DataMember]
        [Display]
        public bool LocalIsMotorErrored { get; set; }


        /// <summary>
        /// Read error code from motor
        /// </summary>
        /// <returns>Error code</returns>
        public bool ReadStallStatus()
        {
            bool isStalled = false;
            string commandText = ConstructReadMotorParameterText(MDriveCommandPrefixes.Stall);
            int isStalledNumeric = WriteReadAndTryParseMotorParam<int>(commandText);

            isStalled = Convert.ToBoolean(isStalledNumeric);

            LocalIsStalled = isStalled;

            return LocalIsStalled;
        }

        /// <summary>
        /// Read error code from motor
        /// </summary>
        /// <returns>Error code</returns>
        public int ReadErrorCode()
        {
            int errorCode;
            string commandText = ConstructReadMotorParameterText(MDriveCommandPrefixes.Error);
            string errorCodeText = WriteRead(commandText, false);
            string errorMessage;

            //split and get error text
            string errorTextSection = errorCodeText.Split(new string[] { MDriveSpecialCharacters.ResponseDelimiter }, StringSplitOptions.RemoveEmptyEntries)[1];

            #region PostConditions

            _codeContractDependency.Requires<FormatException>(
                _typeManipulatorDependency.TryParseNumericFromString(errorTextSection, out errorCode),
                "Unable to parse error code from device: " + ComponentName + ". Raw error text: " + errorTextSection);

            _codeContractDependency.Requires<ArgumentOutOfRangeException>(
                MDriveMotorErrors.TryGetValue(errorCode, out errorMessage),
                "Failed to read error code. Error code: " +
                errorCode +
                " not found in error library. Device: " +
                ComponentName);

            #endregion

            LocalErrorMessage = errorMessage;

            return errorCode;
        }

        [DataMember]
        [ComponentState]
        [Display]
        public bool LocalIsStalled { get; set; }

        [DataMember]
        [ComponentState]
        [Display]
        public string LocalErrorMessage { get; set; }

        #endregion

        #region LexiumErrors
        internal static Dictionary<int, string> MDriveMotorErrors = new Dictionary<int, string>()
        {
            { 0, "No errors reported"},
            { 1, "I/O ERROR: Over-current condition on output 1"},
            { 2, "I/O ERROR: Over-current condition on output 2"},
            { 6, "I/O ERROR: An I/O is already set to this type"},
            { 8, "I/O ERROR: Tried to SET IO to an invalid I/O type"},
            { 9, "I/O ERROR: Tried to write to I/O set as input or is TYPED"},
            { 10, "I/O ERROR: Illegal I/O number"},
            { 11, "I/O ERROR: Incorrect CLOCK type"},
            { 12, "I/O ERROR: Input 1 not defined as a capture input"},
            { 20, "DATA ERROR: Tried to set unknown variable or flag"},
            { 21, "DATA ERROR: Tried to set with a value that is invalid or outside allowable range"},
            { 22, "DATA ERROR: VI is greater than or equal to VM"},
            { 23, "DATA ERROR: VM is less than or equal to VI"},
            { 24, "DATA ERROR: Illegal data entered"},
            { 25, "DATA ERROR: Variable or flag is read only"},
            { 26, "DATA ERROR: Variable or flag is not allowed to be incremented or decremented"},
            { 27, "DATA ERROR: Trip not defined"},
            { 28, "DATA ERROR: Trying to redefine a program label or variable"},
            { 29, "DATA ERROR: Trying to redefine a built-in command, variable, or flag"},
            { 30, "DATA ERROR: Unknown label or user variable"},
            { 31, "DATA ERROR: Program label or user variable table is full"},
            { 32, "DATA ERROR: Trying to set a label (LB)"},
            { 33, "DATA ERROR: Trying to SET an instruction"},
            { 34, "DATA ERROR: Trying to execute a variable or flag"},
            { 35, "DATA ERROR: Trying to print illegal variable or flag"},
            { 36, "DATA ERROR: Illegal motor count to encoder count ratio"},
            { 37, "DATA ERROR: Command, variable, or flag not available in drive"},
            { 38, "DATA ERROR: Missing parameter separator"},
            { 39, "DATA ERROR: Trip on position and trip on relative distance not allowed together"},
            { 40, "PROGRAM ERROR: Program not running"},
            { 41, "PROGRAM ERROR: Stack overflow"},
            { 42, "PROGRAM ERROR: Illegal program address"},
            { 43, "PROGRAM ERROR: Tried to overflow program stack"},
            { 44, "PROGRAM ERROR: Program locked"},
            { 45, "PROGRAM ERROR: Trying to overflow program space"},
            { 46, "PROGRAM ERROR: Not in Program Mode"},
            { 47, "PROGRAM ERROR: Tried to write to illegal flash address"},
            { 48, "PROGRAM ERROR: Program execution stopped by I/O set as Stop"},
            { 60, "COMMUNICATIONS ERROR: Tried to enter an unknown command"},
            { 61, "COMMUNICATIONS ERROR: Trying to set illegal BAUD rate"},
            { 62, "COMMUNICATIONS ERROR: IV already pending or IF Flag already TRUE"},
            { 63, "COMMUNICATIONS ERROR: Character over-run"},
            { 70, "SYSTEM ERROR: FLASH check sum fault"},
            { 71, "SYSTEM ERROR: Internal temperature warning"},
            { 72, "SYSTEM ERROR: Internal over temperature disabling drive"},
            { 73, "SYSTEM ERROR: Tried to SAVE while moving"},
            { 74, "SYSTEM ERROR: Tried to initialize parameters (IP) or clear program (CP) while moving"},
            { 76, "SYSTEM ERROR: Microstep resolution set to low, must be greater than min sys. speed"},
            { 77, "SYSTEM ERROR: VM, VI, or SL too great for selected microstep resolution"},
            { 78, "SYSTEM ERROR: Aux power out of range"},
            { 79, "SYSTEM ERROR: V+ out of range"},
            { 80, "MOTION ERROR: HOME switch not defined"},
            { 81, "MOTION ERROR: HOME type not defined"},
            { 82, "MOTION ERROR: Went to both LIMITS and did not find home"},
            { 83, "MOTION ERROR: Reached plus LIMIT switch"},
            { 84, "MOTION ERROR: Reached minus LIMIT switch"},
            { 85, "MOTION ERROR: MA or MR isn't allowed during a HOME and a HOME isn't allowed while the device is in motion"},
            { 86, "MOTION ERROR: Stall detected"},
            { 87, "MOTION ERROR: Not allowed to change AS mode while moving"},
            { 88, "MOTION ERROR: Moves not allowed while calibration is in progress"},
            { 89, "MOTION ERROR: Calibration not allowed while motion is in progress"},
            { 90, "MOTION ERROR: Motion variables are too low switching to EE=1"},
            { 91, "MOTION ERROR: Motion stopped by I/O set as Stop"},
            { 92, "MOTION ERROR: Position error in closed loop"},
            { 93, "MOTION ERROR: MR or MA not allowed while correcting position at end of previous MR or MA"},
            { 94, "MOTION ERROR: Motion commanded while drive disabled"},
            { 95, "MOTION ERROR: Rotation or direction (RD) attempted while axis is in motion"},
            { 96, "MOTION ERROR: Motion attempted while +V is out of range"},
            { 100, "HYBRID ERROR: Configuration test done, encoder resolution mismatch"},
            { 101, "HYBRID ERROR: Configuration test done, encoder direction incorrect"},
            { 102, "HYBRID ERROR: Configuration test done, encoder resolution and direction incorrect"},
            { 103, "HYBRID ERROR: Configuration not done, drive not enabled"},
            { 104, "HYBRID ERROR: Locked rotor"},
            { 105, "HYBRID ERROR: Maximum position count reached"},
            { 106, "HYBRID ERROR: Lead limit reached"},
            { 107, "HYBRID ERROR: Lag limit reached"},
            { 108, "HYBRID ERROR: Lead/lag not zero at the end of a move"},
            { 109, "HYBRID ERROR: Calibration failed because drive not enabled"},
            { 110, "HYBRID ERROR: Make up disabled"},
            { 111, "HYBRID ERROR: Factory calibration failed"}
        };
        #endregion

        #endregion

        #region EncapsulationMethods


        public int GetEncoderDeadBand()
        {
            string commandText = ConstructReadMotorParameterText(MDriveCommandPrefixes.EncoderDeadBand);
            return WriteReadAndTryParseMotorParam<int>(commandText);

        }

        public void SaveStateToNVM()
        {
            string commandText = ConstructWriteMotorCommandText(MDriveCommandPrefixes.SaveToNVM);//commit current state of motor to its non-volatile memory
            WriteRead(commandText, false);
        }

        /// <summary>
        /// Clears the error on the motor
        /// </summary>
        [ComponentAction(
            memberAlias: "Enable Drive",
            memberDescription: "Enable motor drive. This allows power to be transmitted from the motor to the output shaft",
            memberId: "_enableDrive",
            isIndependent: false)]
        public void EnableDrive()
        {

            string commandText = ConstructWriteMotorParameterText(MDriveCommandPrefixes.DriveEnabled, "1");//enable drive to couple drive to output shaft
            WriteRead(commandText, false);
        }

        /// <summary>
        /// Clears the error on the motor
        /// </summary>
        [ComponentAction(
            memberAlias: "Disable Drive",
            memberDescription: "Disable motor drive. This disconnects power transmission from the motor to the output shaft",
            memberId: "_disableDrive",
            isIndependent: false)]
        public void DisableDrive()
        {

            string commandText = ConstructWriteMotorParameterText(MDriveCommandPrefixes.DriveEnabled, "0");//disable drive to free motion of the output shaft
            WriteRead(commandText, false);

        }

        //private void WriteCurrentConfigurationToMotor()
        //{
        //    WriteDynamicsToMotor();//accell, decel, etc
        //    WriteEncoderEnabled(LocalIsEncoderEnabled);//enabled/disable encoder
        //    WriteOutputConfigurations();//commit digital output settings
        //    WriteInputConfigurations();//commit digital input settings
        //}

        //private void WriteDynamicsToMotor()
        //{
        //    WriteMicroStepResolution(LocalMicroStepResolution);
        //    WriteMotorAngularAcceleration(LocalAngularAcceleration);
        //    WriteMotorAngularDeceleration(LocalAngularDeceleration);
        //    WriteInitialAngularVelocity(LocalInitialAngularVelocity);
        //    WriteMotorMaxAngularVelocity(LocalMaxAngularVelocity);
        //    WritePercentMaxHoldingCurrent(LocalPercentMaxHoldingCurrent);
        //    WritePercentMaxRunningCurrent(LocalPercentMaxRunningCurrent);
        //}


        protected string ConstructReadMotorParameterText(string motorParam)
        {
            #region PreConditions
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(motorParam),
                "Failed to construct command text for device: " +
                ComponentName +
                " because specified motor param was null or empty.");
            #endregion

            string commandText = MDriveCommandPrefixes.PrintParameter + " " + motorParam + MDriveSpecialCharacters.TerminationCharacter;


            #region PostConditions
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(commandText),
                "Failed to construct command text for device: " +
                ComponentName +
                " because specified commandText output was null or empty.");
            #endregion

            return commandText;
        }

        protected string ConstructWriteMotorParameterText(string motorParam, string newValue)
        {
            #region PreConditions
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(motorParam) || !string.IsNullOrEmpty(newValue),
                "Failed to construct command text for device: " +
                ComponentName +
                " because specified motor param or new value was null or empty");
            #endregion

            //if the new value is null or empty, remove the assignment operator
            string commandText = motorParam + MDriveSpecialCharacters.SetterOperator + newValue + MDriveSpecialCharacters.TerminationCharacter;

            #region PostConditions
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(commandText),
                "Failed to construct command text for device: " +
                ComponentName +
                " because specified commandText output was null or empty");
            #endregion

            return commandText;
        }

        protected string ConstructWriteMotorCommandText(string commandText)
        {
            #region PreConditions
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(commandText), "Failed to construct command text for device: " +
                ComponentName +
                " because specified commandText was null or empty");
            #endregion

            string formattedCommandText = commandText + MDriveSpecialCharacters.TerminationCharacter;

            #region PostConditions
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(formattedCommandText),
                "Failed to construct command text for device: " +
                ComponentName +
                " because specified commandText output was null or empty");

            #endregion

            return formattedCommandText;
        }


        /// <summary>
        /// Checks if the device is executing a program or moving
        /// </summary>
        /// <returns>success or fail</returns>
        protected bool IsMotorBusy()
        {
            bool isDeviceBusy;

            string isBusyCommandText = ConstructReadMotorParameterText(MDriveCommandPrefixes.IsProgramExecuting);
            string isMovingCommandText = ConstructReadMotorParameterText(MDriveCommandPrefixes.IsMotorMoving);
            int isBusyTextResponse = WriteReadAndTryParseMotorParam<int>(isBusyCommandText);//ask motor if program is executing
            int isMovingTextResponse = WriteReadAndTryParseMotorParam<int>(isMovingCommandText);//ask motor if it is moving
            isDeviceBusy = !(isBusyTextResponse == 0 && isMovingTextResponse == 0);//set routine active flag

            return isDeviceBusy;
        }

        /// <summary>
        /// Check text for error character
        /// </summary>
        /// <param name="readResponse">text read from motor</param>
        /// <returns>is error character in supplied text</returns>
        protected bool IsResponseSuccess(string readResponse)
        {
            #region PreConditions

            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(readResponse),
                "Failed to evaluate response text for success. Response text cannot be empty. Device: " +
                ComponentName);

            #endregion

            bool isSuccessResponse = false;

            //ensure has success character
            isSuccessResponse = readResponse.Contains(MDriveSpecialCharacters.ResponseDelimiter) && readResponse.Contains(MDriveSpecialCharacters.SuccessResponseCharacter);

            //if it has success char
            if (isSuccessResponse)
            {
                //split response by delimiter
                var responseSections = readResponse.Split(new string[] { MDriveSpecialCharacters.ResponseDelimiter }, StringSplitOptions.RemoveEmptyEntries);
                //ensure no error character
                isSuccessResponse = !responseSections.Any(s => s.Equals(MDriveSpecialCharacters.ErrorResponseCharacter, StringComparison.Ordinal));
            }

            return isSuccessResponse;

        }


        /// <summary>
        /// write command then read out response with hault for remote execution and check for error in repsonse
        /// </summary>
        /// <param name="commandMessage"></param>
        /// <param name="haultForRemoteAction"></param>
        /// <returns name="response"></returns>
        protected string WriteRead(string commandMessage, bool haultForRemoteAction)
        {
            string response = "Unknown";

            //write message
            Write(commandMessage);
            //read response
            response = Read();

            //if need to hault for remote action
            if (haultForRemoteAction)
            {
                //instantiate stopwatch for timeout
                Stopwatch watch = new Stopwatch();
                //start timing
                watch.Start();

                //loop and pause until either [routine is no longer active and the motor is not moving], timeout expires, or active check fails
                while (IsMotorBusy())
                {
                    _codeContractDependency.Requires<TimeoutException>(
                        watch.ElapsedMilliseconds < RemoteActionTimeout,
                        "Timeout occurred while waiting for device to complete actuation. Device: " +
                        ComponentName);
                }
            }

            #region PostConditions
            //was the response a success message
            bool isresponseSuccess = IsResponseSuccess(response);
            //if it wasnt
            if (!isresponseSuccess)
            {
                string errorMessage = GetError();
                _codeContractDependency.Requires<InvalidOperationException>(
                    isresponseSuccess,
                    "Device: " +
                    ComponentName +
                    " returned an error: " +
                    errorMessage +
                    " for command: " +
                    commandMessage);
            }

            #endregion


            return response;
        }

        protected T WriteReadAndTryParseMotorParam<T>(string requestText)
            where T : struct
        {
            #region PreConditions
            _codeContractDependency.Requires<ArgumentNullException>(
                !string.IsNullOrEmpty(requestText),
                "Failed to write command to device. Command text cannot be empty. Device: " +
                ComponentName);
            #endregion
            string readText = WriteRead(requestText, false);
            T readValue = default(T);

            string[] splitResponse = readText.Split(new string[] { MDriveSpecialCharacters.ResponseDelimiter }, StringSplitOptions.RemoveEmptyEntries);
            string numericPortion = splitResponse[1];
            bool isParsed = _typeManipulatorDependency.TryParseNumericFromString(numericPortion, out readValue);

            #region PostConditions

            _codeContractDependency.Requires<FormatException>(
                isParsed,
                "Failed to parse request value from device: " +
                ComponentName +
                ". Raw request text: " +
                requestText +
                ". Raw response text: " +
                readText +
                ". split text: " +
                numericPortion +
                " read n :" +
                readValue +
                " Type " +
                typeof(T).ToString());

            #endregion
            return readValue;
        }


        #endregion

        #region IBidirectionalActuatorAdapter
        public override void InjectServiceProvider(IServiceProvider servProv)
        {
            base.InjectServiceProvider(servProv);
            _typeManipulatorDependency = (ITypeManipulator)serviceProviderDep.GetService(typeof(ITypeManipulator));
        }

        public override void ReadState()
        {
            //read all values from motor
            //ReadEncoderEnabled();
            ReadErrorCode();
            //ReadAndUpdateAnalogInput();
            //ReadAndUpdateDigitalInputs();
            //ReadInitialAngularVelocity();
            ReadIsMotorErrored();
            //ReadMicroStepResolution();
            //ReadMotorAngularAcceleration();
            //ReadMotorAngularDeceleration();
            //ReadMotorMaxAngularVelocity();
            //ReadPercentMaxHoldingCurrent();
            //ReadPercentMaxRunningCurrent();
            ReadPosition();

        }
        public override void CommitConfiguredState()
        {
            //WriteCurrentConfigurationToMotor();//write all settings to motor

        }

        public int GetPosition()
        {

            int pos = ReadPosition();

            return pos;
        }



        /// <summary>
        /// Clears the error on the motor
        /// </summary>
        [ComponentAction(
            memberAlias: "Get Error",
            memberDescription: "Get the current error on the motor",
            memberId: "_GetError",
            isIndependent: false)]
        public override string GetError()
        {

            string errorMessage;
            int motorErrorCode = ReadErrorCode();

            if (!MDriveMotorErrors.TryGetValue(motorErrorCode, out errorMessage))
            {
                errorMessage = "Error : " + motorErrorCode + " not found in device: " + ComponentName + " error dictionary.";
            }

            return errorMessage;
        }

        /// <summary>
        /// Clears the error on the motor
        /// </summary>
        [ComponentAction(
            memberAlias: "Clear Stall",
            memberDescription: "Clears stall flag on motor",
            memberId: "_clearStall",
            isIndependent: false)]
        public void ClearStall()
        {
            int clearStallCode = 0;
            string commandText = ConstructWriteMotorParameterText(MDriveCommandPrefixes.Stall, clearStallCode.ToString());
            WriteRead(commandText, false);

            //get stall status
            ReadStallStatus();

        }

        /// <summary>
        /// Clears the error on the motor
        /// </summary>
        [ComponentAction(
            memberAlias: "Clear Errors",
            memberDescription: "Clear software errors on the motor",
            memberId: "_clearErrors",
            isIndependent: false)]
        public void ClearErrorCode()
        {
            int clearedErrorCode = 0;
            string commandText = ConstructWriteMotorParameterText(MDriveCommandPrefixes.Error, clearedErrorCode.ToString());
            WriteRead(commandText, false);
            string errorMessage;

            #region postConditions
            _codeContractDependency.Requires<ArgumentOutOfRangeException>(
                MDriveMotorErrors.TryGetValue(0, out errorMessage),
                "Unable to clear error message. No clear error message found in error library. Device: " +
                ComponentName);
            #endregion

            LocalErrorMessage = errorMessage;
        }

        [ComponentAction(
            memberAlias: "Home",
            memberDescription: "Home motor and zero position",
            memberId: "_home",
            isIndependent: false)]
        public void Home()
        {
            #region CodeContractPreconditions
            _codeContractDependency.Requires<ObjectDisposedException>(
                !disposed,
                "Failed during home. Device: " +
                ComponentName +
                " has already been disposed. Please create a new instance to operate on it.");

            _codeContractDependency.Requires<InvalidOperationException>(
                IsConnected(),
                "Failed to Home. " +
                ComponentName +
                " not connected!");

            #endregion

            string commandText = ConstructWriteMotorCommandText(MDriveCommandPrefixes.Home);
            WriteRead(commandText, true);

            string commandText2 = ConstructWriteMotorParameterText(MDriveCommandPrefixes.Position, "0");
            WriteRead(commandText2, true);
        }

        /// <summary>
        /// Actuate continuously
        /// </summary>
        /// <param name="stepsPerSecond">The speed in steps per second to actuate the motor. Can be negative</param>
        [ComponentAction(
            memberAlias: "Actuate Continuously",
            memberDescription: "Actuate the motor continuously at the specified angular velocity(steps per second)",
            memberId: "_actuate",
            isIndependent: false)]
        public void Actuate(
            [ComponentActionParameter("Steps per second", "The peak angular velocity at which to actuate", "_stepsPerSecond")]
            int stepsPerSecond)
        {
            string commandText = ConstructWriteMotorParameterText(MDriveCommandPrefixes.SlewAxis, stepsPerSecond.ToString());
            WriteRead(commandText, false);
        }

        /// <summary>
        /// Actuate motor to specified position
        /// </summary>
        /// <param name="stepsPerSecond"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        [ComponentAction(
            memberAlias: "Actuate to Position",
            memberDescription: "Actuate the motor to specified position(steps or encoder steps)",
            memberId: "_servo_actuate",
            isIndependent: false)]
        public int ServoActuate(
            [ComponentActionParameter("Target Position", "The target position to which the motor should actuate", "_position")]
            int position)
        {
            WritePosition(position);
            return ReadPosition();
        }

        [ComponentAction(
            memberAlias: "Initialize",
            memberDescription: "Connect, stop, write current config, and home",
            memberId: "_initialize",
            isIndependent: false)]
        public override void Initialize()
        {

            ClearErrorCode();//clear all errors
            Stop();
            ClearStall();
            ClearErrorCode();
            //Reset();//reset device
            Home();//home device

            //WriteEchoMode(LocalEchoMode);//write default echo mode

            //Stop();//stop the motor
            //WriteCurrentConfigurationToMotor();//write all settings to motor
            //if has encoder
            //if (LocalHasEncoder)
            //{
            //    //confirm encoder settings
            //    string configTest = ConstructWriteMotorCommandText(LexiumCommandPrefixes.SystemConfigurationTest);//construct system config test text to test encoder resolution/direction
            //    WriteRead(configTest, false);//send system config test command to motor
            //}

        }

        [ComponentAction(
            memberAlias: "Pause",
            memberDescription: "Halts all motion and programs",
            memberId: "_pause",
            isIndependent: false)]
        public override void Pause()
        {
            string commandText = ConstructWriteMotorCommandText(MDriveCommandPrefixes.PauseProgramExecution);
            WriteRead(commandText, false);
        }

        /// <summary>
        /// Resume actuation of the motor
        /// </summary>
        /// <returns>success or fail</returns>
        [ComponentAction(
            memberAlias: "Resume",
            memberDescription: "Resume motion or program",
            memberId: "_resume",
            isIndependent: false)]
        public override void Resume()
        {
            string commandText = ConstructWriteMotorCommandText(MDriveCommandPrefixes.ResumeProgramExecution);
            WriteRead(commandText, false);
        }

        [ComponentAction(
            memberAlias: "Soft reset",
            memberDescription: "Stops motion and programs. Clears current configuration.",
            memberId: "_reset",
            isIndependent: false)]
        public override void Reset()
        {
            string commandText = ConstructWriteMotorCommandText(MDriveCommandPrefixes.ResetMotor);
            Write(commandText);
            TerminationCharacters = new string[] { MDriveSpecialCharacters.SuccessResponseCharacter };
            Read();
            TerminationCharacters = new string[] { MDriveSpecialCharacters.ErrorResponseCharacter, MDriveSpecialCharacters.SuccessResponseCharacter };
        }

        [ComponentAction(
            memberAlias: "Shut down",
            memberDescription: "Stops motion and programs and disconnects",
            memberId: "_shutdown",
            isIndependent: false)]
        public override void ShutDown()
        {
            Stop();//stop executing programs and motion
            Disconnect();//disconnect from motor
        }

        [ComponentAction(
            memberAlias: "Stop",
            memberDescription: "Stops motion and programs",
            memberId: "_stop",
            isIndependent: false)]
        public override void Stop()
        {
            //send escape command to stop program execution and motion
            string commandText = ConstructWriteMotorCommandText(MDriveCommandPrefixes.CancelActionAndStopMotor);
            string resp = WriteRead(commandText, false);
            string resp2 = Read();
        }

        #endregion



    }


    [DataContract]
    public class NEMA_17_MDriveMotor : MDriveMotor
    {

        //for serialization
        private NEMA_17_MDriveMotor() : base() { }

        public NEMA_17_MDriveMotor(
            ICodeContractService codeContractDependency,
            ITypeManipulator typeManipulatorDependency)
            : base(codeContractDependency, typeManipulatorDependency)
        {

            //InitializeMembers();
        }


        private void InitializeMembers()
        {
            #region objectInit

            ComponentName = "NEMA 17 Lexium MDrive Motor";
            //LocalDigitalInputs = new List<LexiumDigitalInput>() { new LexiumDigitalInput(), new LexiumDigitalInput(), new LexiumDigitalInput() };
            //LocalDigitalOutputs = new List<LexiumDigitalOutput>() { new LexiumDigitalOutput() };

            #endregion
        }


    }


    [DataContract]
    public class NEMA_23_MDriveMotor : MDriveMotor
    {

        //for serialization
        private NEMA_23_MDriveMotor() : base() { }

        public NEMA_23_MDriveMotor(
            ICodeContractService codeContractDependency,
            ITypeManipulator typeManipulatorDependency)
            : base(codeContractDependency, typeManipulatorDependency)
        {

            InitializeMembers();
        }


        private void InitializeMembers()
        {
            #region objectInit

            ComponentName = "NEMA 23 Lexium MDrive Motor";
            //LocalDigitalInputs = new List<LexiumDigitalInput>() { new LexiumDigitalInput(), new LexiumDigitalInput(), new LexiumDigitalInput(), new LexiumDigitalInput() };
            //LocalDigitalOutputs = new List<LexiumDigitalOutput>() { new LexiumDigitalOutput(), new LexiumDigitalOutput(), new LexiumDigitalOutput() };

            #endregion
        }


    }

    //#region IO_Classes
    //[DataContract]
    //public abstract class LexiumDigital_IO
    //{
    //    #region Ctors
    //    public LexiumDigital_IO()
    //    {
    //        ActiveState = Lexium_IO_Active_State.NONE;
    //        State = IO_State.NONE;

    //    }
    //    #endregion

    //    [DataMember]
    //    [Display]
    //    public Lexium_IO_Active_State ActiveState { get; set; }
    //    [DataMember]
    //    [Display]
    //    public IO_State State { get; set; }

    //}
    //[DataContract]
    //public class LexiumDigitalOutput : LexiumDigital_IO
    //{

    //    #region Ctors

    //    public LexiumDigitalOutput() : base()
    //    {
    //        OutputType = LexiumDigitalOutputType.NONE;
    //        LineIndex = Lexium_Output_Lines.NONE;
    //    }

    //    public LexiumDigitalOutput(
    //        LexiumDigitalOutputType outputType,
    //        Lexium_IO_Active_State activeState,
    //        Lexium_Output_Lines lineIndex)
    //    {
    //        OutputType = outputType;
    //        ActiveState = activeState;
    //        LineIndex = lineIndex;
    //        State = IO_State.NONE;
    //    }

    //    #endregion
    //    [DataMember]
    //    [Display]
    //    public LexiumDigitalOutputType OutputType { get; set; }
    //    [DataMember]
    //    [Display]
    //    public Lexium_Output_Lines LineIndex { get; set; }

    //}
    //[DataContract]
    //public class LexiumDigitalInput : LexiumDigital_IO
    //{

    //    #region Ctors

    //    public LexiumDigitalInput() : base()
    //    {
    //        InputType = LexiumDigitalInputType.NONE;
    //        LineIndex = Lexium_Input_Lines.NONE;
    //    }

    //    public LexiumDigitalInput(
    //        LexiumDigitalInputType inputType,
    //        Lexium_Input_Lines lineIndex,
    //        Lexium_IO_Active_State activeState)
    //    {
    //        LineIndex = lineIndex;
    //        ActiveState = activeState;
    //        InputType = inputType;
    //        State = IO_State.NONE;
    //    }

    //    #endregion

    //    [DataMember]
    //    [Display]
    //    public LexiumDigitalInputType InputType { get; set; }
    //    [DataMember]
    //    [Display]
    //    public Lexium_Input_Lines LineIndex { get; set; }
    //}
    //[DataContract]
    //public class LexiumAnalogInput
    //{
    //    #region Ctors

    //    private LexiumAnalogInput()
    //    {
    //        InputLevel = LexiumAnalogInputLevel.NONE;
    //        InputMode = LexiumAnalogInputMode.NONE;
    //        LineIndex = Lexium_Input_Lines.NONE;
    //    }

    //    public LexiumAnalogInput(
    //        Lexium_Input_Lines lineIndex,
    //        LexiumAnalogInputMode inputMode,
    //        LexiumAnalogInputLevel inputLevel)
    //    {
    //        InputLevel = inputLevel;
    //        InputMode = inputMode;
    //        LineIndex = lineIndex;
    //    }

    //    #endregion

    //    #region Constants
    //    private const double analogInputAbsoluteMax = 4096;
    //    private const double analogInputAbsoluteMin = 0;
    //    private const double analogInputLowLevelCurrentMin = 0;
    //    private const double analogInputLowLevelCurrentMax = 20;
    //    private const double analogInputHighLevelCurrentMin = 4;
    //    private const double analogInputHighLevelCurrentMax = 20;
    //    private const double analogInputLowLevelVoltageMin = 0;
    //    private const double analogInputLowLevelVoltageMax = 5;
    //    private const double analogInputHighLevelVoltageMin = 0;
    //    private const double analogInputHighLevelVoltageMax = 10;
    //    #endregion


    //    [DataMember]
    //    [Display]
    //    public LexiumAnalogInputLevel InputLevel { get; set; }
    //    [DataMember]
    //    [Display]
    //    public LexiumAnalogInputMode InputMode { get; set; }
    //    [DataMember]
    //    [Display]
    //    public double AnalogValue
    //    {
    //        get
    //        {
    //            return analogValue;
    //        }
    //        set
    //        {
    //            if (InputMode == LexiumAnalogInputMode.NONE)
    //            {
    //                analogValue = default(double);
    //            }
    //            else
    //            {
    //                double rangePercent = (value / (double)analogInputAbsoluteMax);
    //                double mappedValue;

    //                if (InputMode == LexiumAnalogInputMode.CURRENT)
    //                {
    //                    if (InputLevel == LexiumAnalogInputLevel.LOW)
    //                    {
    //                        mappedValue = rangePercent * analogInputLowLevelCurrentMax;
    //                    }
    //                    else
    //                    {
    //                        mappedValue = rangePercent * analogInputHighLevelCurrentMax;
    //                    }
    //                }
    //                else
    //                {
    //                    if (InputLevel == LexiumAnalogInputLevel.LOW)
    //                    {
    //                        mappedValue = rangePercent * analogInputLowLevelVoltageMax;
    //                    }
    //                    else
    //                    {
    //                        mappedValue = rangePercent * analogInputHighLevelVoltageMax;
    //                    }
    //                }

    //                analogValue = mappedValue;
    //            }
    //        }
    //    }
    //    private double analogValue;

    //    [DataMember]
    //    public Lexium_Input_Lines LineIndex { get; set; }

    //}

    //#endregion

    #region FixedTextAndEnums
    internal static class MDriveSpecialCharacters
    {
        public const string ErrorResponseCharacter = "?";
        public const string SuccessResponseCharacter = ">";
        public const string TerminationCharacter = "\r";
        public const string SetterOperator = "=";
        public const string ResponseDelimiter = "\r\n";

    }

    //public enum EchoMode
    //{
    //    NONE = -1,
    //    ALL_DEFAULT = 0,
    //    NO_ECHO = 1,
    //    NO_RESPONSE = 2,
    //    QUEUED_ECHO = 3
    //}

    //public enum EncoderEnabled
    //{
    //    NONE = -1,
    //    DISABLED = 0,
    //    ENABLED = 1
    //}

    //public enum Lexium_IO_Active_State
    //{
    //    NONE = -1,
    //    HIGH = 1,
    //    LOW = 0
    //}

    //public enum Lexium_Input_Lines
    //{
    //    NONE = -1,
    //    NONE_INTERNAL = 0,
    //    DIGITAL_INPUT_1 = 1,
    //    DIGITAL_INPUT_2 = 2,
    //    DIGITAL_INPUT_3 = 3,
    //    DIGITAL_INPUT_4 = 4,
    //    ANALOG_INPUT = 5,
    //    ENCODER_INPUT = 6

    //}

    //public enum Lexium_Output_Lines
    //{
    //    NONE = -1,
    //    NONE_INTERNAL = 0,
    //    DIGITAL_OUTPUT_1 = 1,
    //    DIGITAL_OUTPUT_2 = 2,
    //    DIGITAL_OUTPUT_3 = 3
    //}

    //public enum LexiumHomingType
    //{
    //    NONE = -1,
    //    NONE_INTERNAL = 0,
    //    HOME_MINUS_CREEP_POSITIVE = 1,
    //    HOME_MINUS_CREEP_MINUS = 2,
    //    HOME_POSITIVE_CREEP_MINUS = 3,
    //    HOME_POSITIVE_CREEP_POSITIVE = 4

    //}

    //public enum LexiumDigitalInputType
    //{
    //    NONE = -1,
    //    GENERAL_PURPOSE = 0,
    //    HOMING = 1,
    //    POSITIVE_LIMIT = 2,
    //    NEGATIVE_LIMIT = 3,
    //    EXECUTE_ADDRESS_1_PROGRAM = 4,
    //    SOFT_STOP = 5,
    //    PAUSE_RESUME = 6,
    //    JOG_POSITIVE = 7,
    //    JOG_NEGATIVE = 8,
    //    RESET = 11

    //}

    //public enum LexiumDigitalOutputType
    //{
    //    NONE = -1,
    //    NONE_INTERNAL = 0,
    //    GENERAL_PURPOSE = 16,
    //    MOVING = 17,
    //    ERROR = 18,
    //    STALL = 19,
    //    V_CHANGE = 20,
    //    LOCKED_ROTOR = 21,
    //    MOVING_POSITION = 22,
    //    HMT_ACTIVE = 23,
    //    MAKE_UP_ACTIVE = 24,
    //    TRIP_OUT = 25,
    //    ATTENTION = 26

    //}

    //public enum LexiumAnalogInputMode
    //{
    //    NONE = -1,
    //    VOLTAGE = 9,
    //    CURRENT = 10
    //}

    //public enum LexiumAnalogInputLevel
    //{
    //    NONE = -1,
    //    LOW = 0,
    //    HIGH = 1
    //}

    //public enum LexiumMicroStepResolution
    //{
    //    NONE = -1,
    //    NONE_INTERNAL = 0,
    //    PER_STEP_256 = 256,
    //    PER_STEP_128 = 128,
    //    PER_STEP_64 = 64,
    //    PER_STEP_32 = 32,
    //    PER_STEP_16 = 16,
    //    PER_STEP_8 = 8,
    //    PER_STEP_4 = 4,
    //    PER_STEP_2 = 2,
    //    PER_STEP_1 = 1

    //}

    internal static class MDriveCommandPrefixes
    {

        internal const string ProgramMode = "E";
        internal const string EchoMode = "EM";
        internal const string DriveEnabled = "DE";
        internal const string SlewAxis = "SL";
        internal const string Stall = "ST";
        internal const string HoldingCurrent = "HC";
        internal const string RunningCurrent = "RC";
        internal const string Acceleration = "A";
        internal const string Deceleration = "D";
        internal const string SetupInputs = "IS";
        internal const string HoldProgramExecution = "H";
        internal const string ExecuteProgram = "EX";
        internal const string SetupOutputs = "OS";
        internal const string PauseProgramExecution = "PS";
        internal const string ResumeProgramExecution = "RS";
        internal const string Home = "HM 1";
        internal const string IsProgramExecuting = "BY";
        internal const string IsMotorMoving = "MV";
        internal const string ReadInputsAsGroup = "IN";
        internal const string MoveAbsolute = "MA";
        internal const string ReadInternalTemperature = "IT";
        internal const string Error = "ER";
        internal const string MotorSettlingTime = "MT";
        internal const string MoveRelative = "MR";
        internal const string MaximumVelocity = "VM";
        internal const string InitialVelocity = "VI";
        internal const string WarningTemperature = "W";
        internal const string Position = "P";
        internal const string MicrostepResolution = "MS";
        internal const string EncoderEnabled = "EE";
        internal const string PrintParameter = "PR";
        internal const string InputPrefix = "I";
        internal const string OutputPrefix = "O";
        internal const string SystemConfigurationTest = "SC 1";
        internal const string ResetMotor = "\u0003";
        internal const string CancelActionAndStopMotor = "\u001b";
        internal const string SaveToNVM = "S";
        internal const string EncoderDeadBand = "DB";


    }

    #endregion


}


