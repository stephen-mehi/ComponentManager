using DropQuant_Remote;
using System;
using System.Collections.Generic;
using System.IO;

namespace LunaticAdapter
{
    class LunaticAPI : ILunaticAPI
    {
        public LunaticAPI(string IPAddress, int port)
        {


            dropQuant = new DropQuant(IPAddress, port);
        }

        private readonly DropQuant dropQuant;

        private Dictionary<int, string> ErrorLibrary { get; } = new Dictionary<int, string>()
        {
            {-401, "File listed as an argument could not be found/read"},
            {-400, "Not all arguments are listed"},
            {-300, "Unknown Command"},
            {-202, "Internal DLL error: Could not convert the status code to integer"},
            {-201, "Internal DLL error: Could not convert the DQ return string to the desired parameters"},
            {-200, "Could not connect over TCP"},
            {-111, "Instrument in incorrect state"},
            {-106, "No Plate in the instrument"},
            {-104, "Results can only be exported if all Lunatic Plates are measured"},
            {-103, "Incorrect argument used, Lunatic Plate ID is already measured"},
            {-102, "Incorrect argument used, Lunatic Plate ID not valid"},
            {-100, "Access interrupted by local user interface"},
            {-99, "Timeout"},
            {-52, "Access and connection, could not perform command, tray is moving"},
            {-31, "Could not perform command, measurement busy"},
            {-24, "Access and connection, measurement failed"},
            {-22, "Access and connection, measurement failed to initialize"},
            {-17, "Bar code reader timeout"},
            {-16, "No bar code reader avilable"},
            {-15, "Bar code not valid"},
            {-14, "Timeout for going to open tray position"},
            {-13, "No samples defined"},
            {-12, "No pump profiles found"},
            {-11, "No pump profiles found"},
            {-10, "Error reading samples"},
            {-9, "Error reading experiment definition"},
            {-5, "Tray not in right position"},
            {-4, "Could not close tray"},
            {-3, "Could not open tray"},
            {-2, "Instrument in incorrect state"},
            {-1, "No access"},
            {0, "Successful"},
            {1, "Access already achieved"},
            {3, "Tray was already open"},
            {4, "Tray was already closed"},
            {20, "Access free"},
            {21, "Access and connection, no measurement"},
            {23, "Access and connection, measurement started"},
            {25, "Access and connection, measurement successful"},
            {30, "Access and connection, measurement initializing"},
            {31, "Access and connection, measurement busy"},
            {32, "Access and connection, load next Lunatic Plate"},
            {33, "Access and connection, measurement paused"},
            {50, "Access and connection, tray is open"},
            {51, "Access and connection, tray is closed"},
            {52, "Access and connection, tray is moving"},
            {999, "Lunatic Client Exit accepted"},

        };



        #region ILunaticAPIImplementation

        public string GetLunaticError(int errorCode)
        {
            string errorMessage;
            bool gotValue = ErrorLibrary.TryGetValue(errorCode, out errorMessage);
            if (!gotValue)
                errorMessage = "No error message found for error code: " + errorCode;
            return errorMessage;
        }


        public int RequestAccess()
        {
            int retval = dropQuant.DQ_Request_Access();
            return retval;
        }

        public int ReleaseAccess()
        {
            int retval = dropQuant.DQ_Release_Access();
            return retval;
        }

        public int OpenTray()
        {
            int retval = dropQuant.DQ_Open_Tray();
            return retval;
        }

        public int CloseTray()
        {
            int retval = dropQuant.DQ_Close_Tray();
            return retval;
        }

        public int GetStatus(out string measurement_info)
        {
            int retval = dropQuant.DQ_Get_Status(out measurement_info);
            if (string.IsNullOrEmpty(measurement_info))
                measurement_info = "No measurement info available";
            return retval;
        }

        public int DefineExperiment(string experimentdefinition, string sampledefinition, out string[] plateIDs)
        {
            int retval = dropQuant.DQ_Define_Experiment(experimentdefinition, sampledefinition, out plateIDs);
            return retval;
        }

        public int Measure(string plateID)
        {
            int retval = dropQuant.DQ_Measure(plateID);
            return retval;
        }

        public int Abort_Measurement()
        {
            int retval = dropQuant.DQ_Abort_Measurement();
            return retval;
        }

        public int GetResults(string resultsparameters, string plateID, out string results, out string experimentpath)
        {
            int retval = dropQuant.DQ_Get_Results(resultsparameters, plateID, out results, out experimentpath);
            return retval;
        }

        public int ReadBarCode(out string barcode)
        {
            int retval = dropQuant.DQ_Read_Bar_Code(out barcode);
            return retval;
        }

        public int ReadBarcode(out string barcode, out bool valid, out string dropplatetype)
        {
            int retval = dropQuant.DQ_Read_Bar_Code(out barcode, out valid, out dropplatetype);
            return retval;
        }

        public string GetLastInternalError()
        {
            string retval = dropQuant.DQ_GetLastInternalError();
            return retval;
        }

        #endregion


    }

    public class FileUtility : IFileUtility
    {

        public string GetContents(string filePath)
        {
            #region Validation

            if (string.IsNullOrEmpty(filePath))
                throw new NullReferenceException("File path cannot be null or empty.");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found at path: " + filePath);
            #endregion

            var contents = File.ReadAllText(filePath);

            #region postConditions

            if (string.IsNullOrEmpty(contents))
                throw new NullReferenceException("File was empty at: " + filePath);

            #endregion

            return contents;
        }

    }

}
