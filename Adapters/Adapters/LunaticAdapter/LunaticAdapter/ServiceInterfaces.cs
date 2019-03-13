namespace LunaticAdapter
{
    public interface ILunaticAPI
    {
        int RequestAccess();

        int ReleaseAccess();

        int OpenTray();

        int CloseTray();

        int GetStatus(out string measurement_info);

        int DefineExperiment(string experimentdefinition, string sampledefinition, out string[] plateIDs);

        int Measure(string plateID);

        int Abort_Measurement();

        int GetResults(string resultsparameters, string plateID, out string results, out string experimentpath);

        int ReadBarCode(out string barcode);

        int ReadBarcode(out string barcode, out bool valid, out string dropplatetype);

        string GetLastInternalError();

        string GetLunaticError(int errorCode);

    }



    public interface IFileUtility
    {



        string GetContents(string filePath);

    }

}
