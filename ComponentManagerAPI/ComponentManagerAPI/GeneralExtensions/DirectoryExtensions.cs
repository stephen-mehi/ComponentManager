using System.IO;

namespace ComponentManagerAPI.GeneralExtensions
{
    public static class DirectoryExtensions
    {
        public static void DeleteAllFilesAndSubdirectories(this DirectoryInfo directory)
        {
            foreach (System.IO.FileInfo file in directory.GetFiles("*.*", SearchOption.AllDirectories)) file.Delete();
            foreach (System.IO.DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
        }

    }
}
