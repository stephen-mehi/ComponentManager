using AdapterBaseClasses;
using ComponentInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DataUploader
{
    /// <summary>
    /// Base implementation of a class providing tooling for posting transfer data to a data store
    /// </summary>
    [DataContract]
    public abstract class DataUploaderBase : IComponentAdapter
    {
        /// <summary>
        /// 
        /// </summary>
        public DataUploaderBase()
        {

            ComponentName = "Base Data Uploader";
        }

        #region FIELDS

        private bool disposed = false;

        #endregion

        #region ENCAPSULATION_METHODS

        /// <summary>
        /// 
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            //no resources yet
        }

        #endregion

        #region IComponentAdapter
        /// <summary>
        /// 
        /// </summary>
        public string ComponentName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public virtual void CommitConfiguredState()
        {
            //nothing
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void Connect()
        {
            //nothing
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void Disconnect()
        {
            //NOTHING
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            if (disposed == true)
            {
                throw new ObjectDisposedException("");
            }
            Dispose(true);
            disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual string GetError()
        {
            //NOTHING
            return "This virtual component is incapable of reporting errors";
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void Initialize()
        {
            //NOTHING
        }


        /// <summary>
        /// 
        /// </summary>
        public virtual bool IsConnected()
        {
            try
            {
                Connect();
            }
            catch (Exception)
            {

                return false;
            }

            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        public virtual void Pause()
        {
            //nothing
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void ReadState()
        {
            //nothing
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void Reset()
        {
            //NOTHING
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void Resume()
        {
            //NOTHING
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void ShutDown()
        {
            //NOTHING
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void Stop()
        {
            //NOTHING
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void InjectServiceProvider(IServiceProvider servProv)
        {
            return;
        }

        #endregion

    }

    /// <summary>
    /// 
    /// </summary>
    public interface IDataUploader
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        void UploadFileData(string filePath, string stagingPath);

    }

    /// <summary>
    /// 
    /// </summary>
    [DataContract]
    public abstract class WebServiceUploader : DataUploaderBase, IDataUploader
    {

        private class CustomStringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        /// <summary>
        /// CTOR
        /// </summary>
        public WebServiceUploader()
        {
            ComponentName = "Transfer Data Uploader";

        }

        #region PROPERTIES

        /// <summary>
        /// The string used to decode encoded whitespaces in the uploaded file
        /// </summary>
        [DisplayName("File Whitespace Encoding String")]
        [DataMember]
        [Display]
        public string FileWhitespaceEncodingString { get; set; }

        /// <summary>
        /// Uri of the service used to parse and transform transfer file
        /// </summary>
        [DisplayName("File Transformation URI")]
        [DataMember]
        [Display]
        public string FileTransformationUri { get; set; }

        /// <summary>
        /// Uri of the service that commits the transformed file to a data store
        /// </summary>
        [DisplayName("Upload Service URI")]
        [DataMember]
        [Display]
        public string UploadUri { get; set; }

        /// <summary>
        /// The current file parser rules profile
        /// </summary>
        [DisplayName("Parser Rules Profile")]
        [DataMember]
        [Display]
        public IParserRules ParserRulesProfile { get; set; }


        #endregion

        #region ENCAPSULATION_METHODS

        /// <summary>
        /// Helper used to ping remote service hosts to ensure availability. Throws if unavailable
        /// </summary>
        protected void ConfirmRemoteHostAvailability()
        {

            //make collection of hosts
            var uriCollection = new List<string>() { this.UploadUri, this.FileTransformationUri };

            //iterate over uris
            var pingTasks = uriCollection
                .GroupBy(u => new Uri(u).Host)
                .Select((g) =>
                {
                    //get current host
                    var tempHost = g.Key;

                    //confirm resolution of host segment of both services
                    if (string.IsNullOrEmpty(tempHost))
                        throw new ArgumentNullException("Failed to connect to remote host of a service. Unable to resolve remote host name from uri: " + g.First());
                    //create new ping obj
                    var tempPing = new Ping();
                    //return anonymous obj
                    return new { _pingTask = tempPing.SendPingAsync(tempHost), _host = tempHost };
                });

            //wait on all ping tasks concurrently
            Task.WaitAll(pingTasks.Select(p => p._pingTask).ToArray());

            foreach (var pingTask in pingTasks)
            {
                //confirm successful ping echo
                if (pingTask._pingTask.Result.Status != IPStatus.Success)
                    throw new IOException("Failed to connect to remote host: " + pingTask._host);

            }
        }

        /// <summary>
        /// Helper that validates class level state depended on by request methods
        /// </summary>
        protected void RequestPreValidation()
        {

            if (string.IsNullOrEmpty(FileTransformationUri))
                throw new ArgumentNullException("Failed during class invariant check. File data tranformation uri cannot be null");

            if (string.IsNullOrEmpty(UploadUri))
                throw new ArgumentNullException("Failed during class invariant check. Upload uri cannot be null");
        }

        protected async Task<HttpResponseMessage> PostStringAsync(string content, string uri)
        {
            #region preconditions

            if (string.IsNullOrEmpty(content))
                throw new ArgumentNullException("Failed to post string. String to post cannot be null");

            if (string.IsNullOrEmpty(uri))
                throw new ArgumentNullException("Failed to post object. Uri cannot be null");

            #endregion


            //using control for string writer
            using (HttpClientHandler requestHandler = new HttpClientHandler() { Credentials = new NetworkCredential(userName: "FPTApplications", password: "F!vePr!me", domain: "CORP") })
            using (HttpClient requestObj = new HttpClient(requestHandler))
            {
                //prepare web client obj
                //HttpClient request = new HttpClient();
                requestObj.BaseAddress = new Uri(uri);

                //post obj xml async
                HttpResponseMessage response = await requestObj.PostAsync(requestUri: "", content: new StringContent(content: content, encoding: Encoding.ASCII, mediaType: "application/xml"));

                return response;
            }

        }

        /// <summary>
        /// Serialize object to uri endpoint
        /// </summary>
        /// <returns>Response object from web request</returns>
        protected async Task<HttpResponseMessage> PostObjectAsync(object obj, string uri)
        {

            #region preconditions

            if (obj == null)
                throw new ArgumentNullException("Failed to post object. Post object cannot be null");

            #endregion

            //declare vars in scope
            string objXml;

            //prepare serializer obj
            XmlSerializer objSerializer = new XmlSerializer(obj.GetType());

            //using control for string writer
            using (StringWriter writer = new CustomStringWriter())
            {
                //serialize obj to xml string
                objSerializer.Serialize(textWriter: writer, o: obj);
                //get string
                objXml = writer.ToString();
            }


            return await PostStringAsync(content: objXml, uri: uri);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="removalStrings"></param>
        /// <returns></returns>
        protected string GetSanitizedFileContents(string path, string[] removalStrings = null)
        {
            #region preconditions

            string failurePrefix = "Failed component: " + ComponentName + " Failed to get sanitized file contents. ";

            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(failurePrefix + "Path to file cannot be empty");
            if (!File.Exists(path))
                throw new FileNotFoundException(failurePrefix + "File not found at: " + path);

            #endregion

            //read all text from file
            string fileContents = File.ReadAllText(path: path);

            if (string.IsNullOrEmpty(fileContents))
                throw new NullReferenceException(failurePrefix + "File was empty.");

            string cleanedContents = fileContents;

            //iterate over removal strings
            foreach (var removeStr in removalStrings)
            {
                //replace string with empty string
                cleanedContents = fileContents.Replace(removeStr, string.Empty);
            }

            //decode file contents based on configured whitespace encoding
            string decodedContents = DecodeStringWhitespace(strContent: cleanedContents, encodingString: FileWhitespaceEncodingString);

            return cleanedContents;

        }

        /// <summary>
        /// Replaces an encoding string with whitespace
        /// </summary>
        /// <param name="strContent">The content to be decoded</param>
        /// <param name="encodingString">the encoding string to replace with whitespace</param>
        /// <returns></returns>
        protected string DecodeStringWhitespace(string strContent, string encodingString)
        {
            #region preconditions

            string failPrefix = "Failed to decode string whitespace. "; 

            if (string.IsNullOrEmpty(encodingString))
                throw new ArgumentNullException(nameof(encodingString), failPrefix + "Encoding string cannot be empty.");
            if (string.IsNullOrEmpty(encodingString))
                throw new ArgumentNullException(nameof(strContent), failPrefix + "String to be decoded cannot be empty.");

            #endregion

            string decodedString = strContent.Replace(oldValue: encodingString, newValue: " ");

            return decodedString;
        }

        /// <summary>
        /// Delete a temp file if it exists
        /// </summary>
        /// <param name="tempFilePath"></param>
        protected void ClearTempFile(string tempFilePath)
        {
            if (string.IsNullOrEmpty(tempFilePath))
                throw new ArgumentNullException("Failed to clear temp file. Path cannot be null");

            //if file exists
            if (File.Exists(tempFilePath))
            {
                File.Delete(path: tempFilePath);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        /// <param name="overWrite"></param>
        protected virtual void ValidateTransferFileInput(string sourcePath, string destinationPath, bool overWrite = false)
        {
            string failurePrefix = "Failed component: " + ComponentName + " Failed to validate input for Sanitize And Transfer File method. ";

            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException(failurePrefix + "Source path cannot be empty");

            if (string.IsNullOrEmpty(destinationPath))
                throw new ArgumentNullException(failurePrefix + "Source path cannot be empty");

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException(failurePrefix + "Source file not found: " + sourcePath);

            if (!overWrite && File.Exists(destinationPath))
                throw new AmbiguousMatchException(failurePrefix + "destination path specified already exists: " + destinationPath);
        }

        /// <summary>
        /// sanitize file contents and write to new file 
        /// </summary>
        protected virtual void SanitizeAndTransferFile(string sourcePath, string destinationPath, bool overWrite = false, string[] removalStrings = null)
        {
            #region preconditions

            ValidateTransferFileInput(sourcePath: sourcePath, destinationPath: destinationPath, overWrite: overWrite);

            #endregion

            //get cleaned file contents
            string cleanContents = GetSanitizedFileContents(path: sourcePath, removalStrings: removalStrings);
            //write cleaned contents to file
            File.WriteAllText(path: destinationPath, contents: cleanContents);

        }

        /// <summary>
        /// 
        /// </summary>
        [ComponentAction(memberAlias: "Upload File", memberDescription: "Transform and upload file data", memberId: "_uploadFileData", isIndependent: false)]
        public void UploadFileData(
            [ComponentActionParameter(memberAlias: "File Path", memberDescription: "Path of the file that should be uploaded", memberId:"_filePath")]
            string filePath,
            [ComponentActionParameter(memberAlias: "Staging File Path", memberDescription: "Staging location of the file before upload", memberId:"_stagingFilePath")]
            string stagingPath)
        {

            #region PRECONDITIONS
            string failurePrefix = "Failed component: " + ComponentName + " Failed to upload file. ";

            //confirm class invariant
            RequestPreValidation();

            if (ParserRulesProfile == null)
                throw new ArgumentNullException(failurePrefix + "Parser rule profile cannot be null");
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(failurePrefix + "File path cannot be empty");
            if (string.IsNullOrEmpty(stagingPath))
                throw new ArgumentNullException(failurePrefix + "Staging file path cannot be empty");

            #endregion

            #region CleanAndTransferFile

            SanitizeAndTransferFile(sourcePath: filePath, destinationPath: stagingPath, overWrite: false, removalStrings: new string[] { "\"" });

            #endregion

            //attempt progression of uploads
            try
            {
                #region TransformFileData

                //update parse rules with file path
                ParserRulesProfile.FilePath = stagingPath;

                //post obj async
                var responseTask = PostObjectAsync(ParserRulesProfile, FileTransformationUri);
                //wait for response
                responseTask.Wait();
                //set response
                HttpResponseMessage response = responseTask.Result;

                //ensure 200
                if (!response.IsSuccessStatusCode)
                {
                    var failTask = response.Content.ReadAsStringAsync();
                    failTask.Wait();
                    string failText = failTask.Result;
                    throw new HttpRequestException(message: failText);
                }

                //read response as string async
                var readTask = response.Content.ReadAsStringAsync();
                //wait for task to complete
                readTask.Wait();
                //get task result
                string transformedFileXml = readTask.Result;

                //ensure response content
                if (string.IsNullOrEmpty(transformedFileXml))
                    throw new NullReferenceException(failurePrefix + "No response content returned from Sample Parser Service");

                #endregion

                #region DataUpload

                var responseTask2 = PostStringAsync(content: transformedFileXml, uri: UploadUri);
                responseTask2.Wait();
                var response2 = responseTask2.Result;

                //ensure 200
                if (!response2.IsSuccessStatusCode)
                {
                    var failTask = response2.Content.ReadAsStringAsync();
                    failTask.Wait();
                    string failText = failTask.Result;
                    throw new HttpRequestException(message: failText);
                }


                #endregion
            }
            finally
            {
                //always clear temp file
                ClearTempFile(tempFilePath: stagingPath);
            }

        }



        #endregion

        #region OVERRIDE_IComponentManager

        /// <summary>
        /// 
        /// </summary>
        public override void Connect()
        {
            //call base
            base.Connect();

            ConfirmRemoteHostAvailability();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override bool IsConnected()
        {
            bool baseConnected = base.IsConnected();
            ConfirmRemoteHostAvailability();

            return baseConnected;
        }

        #endregion

    }

    /// <summary>
    /// Class providing tooling to post transfer data to a data store
    /// </summary>
    [DataContract]
    public class SampleTransferUploader : WebServiceUploader
    {
        /// <summary>
        /// 
        /// </summary>
        public SampleTransferUploader()
        {
            ParserRulesProfile = new SampleTransferParserInfo();
            ComponentName = "Sample Transfer Uploader";

        }


    }


    /// <summary>
    /// Class providing tooling to post transfer data to a data store
    /// </summary>
    [DataContract]
    public class ContainerUploader : WebServiceUploader
    {
        /// <summary>
        /// 
        /// </summary>
        public ContainerUploader()
        {
            ParserRulesProfile = new ContainerParserInfo();
            ComponentName = "Container Transfer Uploader";

        }

    }

    /// <summary>
    /// 
    /// </summary>
    public class DataSetUploader : WebServiceUploader
    {

        /// <summary>
        /// 
        /// </summary>
        public DataSetUploader()
        {
            ParserRulesProfile = new SampleInstanceDatasetParserInfo();
            ComponentName = "Dataset Uploader";

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="valueColumn"></param>
        /// <param name="defaultValue"></param>
        [ComponentAction(memberAlias: "Add Data Set Attribute", memberDescription: "Adds an additional dataset attribute that will be available for configuration in the profile page", memberId: "_addDataSetAttribute", isIndependent: false)]
        public void AddDataSetAttribute(
            [ComponentActionParameter(memberAlias: "Name", memberDescription: "Name of data set attribute", memberId:"_name")]
            string name,
            [ComponentActionParameter(memberAlias: "Value Column", memberDescription: "The column in the file where the value can be found", memberId:"_valueColumn")]
            int valueColumn,
            [ComponentActionParameter(memberAlias: "Default Value", memberDescription: "The defualt value if none found", memberId:"_defaultValue")]
            string defaultValue)
        {

            #region preconditions

            string failPrefix = "Failed to add data set attribute. ";

            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), failPrefix + "Data set attribute name cannot be empty");
            if (string.IsNullOrEmpty(defaultValue))
                throw new ArgumentNullException(nameof(defaultValue), failPrefix + "Data set attribute default value cannot be empty");
            if (valueColumn < 1)
                throw new ArgumentNullException(nameof(valueColumn), failPrefix + "Data set attribute value column cannot be less than 1");

            #endregion
            
            //cast 
            var parserProfile = (SampleInstanceDatasetParserInfo)ParserRulesProfile;

            //if list not initialized
            if (parserProfile.DatasetAttributeColumns == null)
                parserProfile.DatasetAttributeColumns = new List<Column>();//set to new list

            //init new column obj
            var col = new Column()
            {
                Name = name,
                Number = valueColumn,
                Value = defaultValue
            };

            //add to list
            parserProfile.DatasetAttributeColumns.Add(col);

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="dsName"></param>
        [ComponentAction(memberAlias: "Delete Data Set Attribute", memberDescription: "Deletes a dataset attribute object from the profile by name", memberId: "_deleteDataSetAttribute", isIndependent: false)]
        public void DeleteDataSetAttribute(
            [ComponentActionParameter(memberAlias: "Dataset Attribute Name", memberDescription: "Name of data set attribute to delete", memberId:"_dsName")]
            string dsName)
        {

            //cast 
            var parserProfile = (SampleInstanceDatasetParserInfo)ParserRulesProfile;

            #region preconditions

            string failPrefix = "Failed to delete data set attribute. ";

            if (string.IsNullOrEmpty(dsName))
                throw new ArgumentNullException(nameof(dsName), failPrefix + "Data set attribute name cannot be empty");
            if (parserProfile.DatasetAttributeColumns == null || parserProfile.DatasetAttributeColumns.Count == 0)
                throw new NullReferenceException(failPrefix + "No dataset attributes exist for this profile");
            if (parserProfile.DatasetAttributeColumns.Where(ds => ds.Name.Equals(dsName, StringComparison.Ordinal)).Count() != 1)
                throw new NullReferenceException(failPrefix + "No dataset attributes exist with name: " + dsName);

            #endregion

            //get position of column that matches by name
            int position = parserProfile.DatasetAttributeColumns.FindIndex(ds => ds.Name.Equals(dsName, StringComparison.Ordinal));

            //remove from list
            parserProfile.DatasetAttributeColumns.RemoveAt(position);

        }

    }

    public class PhDataSetUploader : DataSetUploader
    {
        public PhDataSetUploader()
        {
            ComponentName = "pH Dataset Uploader";
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        /// <param name="overWrite"></param>
        /// <param name="removalStrings"></param>
        protected override void SanitizeAndTransferFile(string sourcePath, string destinationPath, bool overWrite = false, string[] removalStrings = null)
        {

            #region preconditions

            //set failure prefix
            string failurePrefix = "Failed component: " + ComponentName + " . Failed to sanitize and transfer file. ";

            ISampleInstanceDataSetParseRules castedParserRules = ParserRulesProfile as ISampleInstanceDataSetParseRules;

            if (castedParserRules == null)
                throw new InvalidCastException(failurePrefix + "Failed to cast parser rules to interface: " + nameof(ISampleInstanceDataSetParseRules));

            if (castedParserRules.ContainerInfo == null)
                throw new ArgumentNullException(failurePrefix + "Container info cannot be null");

            if (castedParserRules.ContainerInfo.BarcodeColumnNumber < 1)
                throw new ArgumentNullException(failurePrefix + "Barcode column number cannot be less than 1");

            if (castedParserRules.ContainerInfo.Row < 1)
                throw new ArgumentNullException(failurePrefix + "Container row cannot be less than 1");

            if (castedParserRules.ContainerInfo.Col < 1)
                throw new ArgumentNullException(failurePrefix + "Container column cannot be less than 1");

            #endregion


            //call base validation
            base.ValidateTransferFileInput(sourcePath: sourcePath, destinationPath: destinationPath, overWrite: overWrite);
            //call base sanitizing function
            string cleanedContents = base.GetSanitizedFileContents(path: sourcePath, removalStrings: removalStrings);

            if (string.IsNullOrEmpty(cleanedContents))
                throw new NullReferenceException(failurePrefix + "Contents from file were empty after sanitizing.");

            //split cleaned contents into 
            string[] lines = cleanedContents.Split(separator: new string[] { Environment.NewLine }, options: StringSplitOptions.RemoveEmptyEntries);

            if (lines == null || lines.Count() == 0)
                throw new NullReferenceException(failurePrefix + "Contents of file were empty after sanitizing and splitting by return.");


            IEnumerable<string> recordLines;
            //get start/end row
            int startRow = castedParserRules.StartRowNumber;
            int endRow = castedParserRules.EndRowNumber;

            //number of skipped rows
            int nSkippedRows = startRow - 1;
            //skip necessary rows
            recordLines = lines.Skip(nSkippedRows);

            //if end row not 0 (meaning process all remaining lines)
            if (endRow != 0)
            {
                //calculate rows to take
                int nTakenRows = recordLines.Count() - nSkippedRows;
                //get that subset
                recordLines = recordLines.Take(nTakenRows);
            }

            if (recordLines == null || recordLines.Count() == 0)
                throw new NullReferenceException(failurePrefix + "File contents were empty after extracting specified range. Start row:" + startRow + " End row: " + endRow);

            //get first line
            var firstLine = recordLines.FirstOrDefault();
            //try to count components of lines
            int? lineLength =
                firstLine?
                .Split(separator: new string[] { "," }, options: StringSplitOptions.None)?
                .Count();

            if (lineLength == null)
                throw new NullReferenceException(failurePrefix + "Failed to count number of component in line. Raw line text: " + firstLine);

            int _lineLength = (int)lineLength;

            //get list of string arrays representing file lines
            List<List<string>> recordLineSegments =
                recordLines
                .Select(l => l.Split(separator: new string[] { "," }, options: StringSplitOptions.None).ToList())
                .ToList();

            //check if all lines have same number of segments
            bool areAllSameLength = recordLineSegments.All(l => l.Count() == _lineLength);
            if (!areAllSameLength)
                throw new ArgumentException(failurePrefix + "All lines did not have the same number of segments.");

            List<string> sTypes = new List<string>();
            List<string> sSubtypes = new List<string>();

            //open connection to db
            using (var database = new FPTAutomation.ADBModel.AutomationDBEntities())
            {

                foreach (var line in recordLineSegments)
                {
                    //get tube barcode
                    string tubeBarcode = line[castedParserRules.ContainerInfo.BarcodeColumnNumber - 1];
                    //get samples
                    var samples = from c in database.Containers
                                  join si in database.SampleInstances on c.ContainerId equals si.ContainerId
                                  join s in database.Samples on si.SampleId equals s.SampleId
                                  where c.Barcode == tubeBarcode
                                  select s;

                    //count samples
                    if (samples.Count() > 1)
                        throw new ArgumentOutOfRangeException(failurePrefix + "More than one sample was found in tube: " + tubeBarcode);

                    //get first
                    var _sample = samples.FirstOrDefault();

                    if (_sample == null)
                        throw new NullReferenceException(failurePrefix + "Failed to find sample in tube: " + tubeBarcode);

                    //add sample name
                    line.Add(_sample.Name);
                    //add type/subtype to collection
                    sTypes.Add(_sample.Type);
                    sSubtypes.Add(_sample.SubType);

                }

            }

            //ensure only one type and subtype
            if (!sTypes.All(s => s == sTypes.FirstOrDefault()))
                throw new ArgumentException(failurePrefix + "The sample types in each tube did not match");

            if (!sSubtypes.All(s => s == sSubtypes.FirstOrDefault()))
                throw new ArgumentException(failurePrefix + "The sample subtypes in each tube did not match");

            //set sample type/subtype
            castedParserRules.SampleInfo.SampleType = sTypes.FirstOrDefault();
            castedParserRules.SampleInfo.SampleSubType = sSubtypes.FirstOrDefault();

            //set correct header, start, and row
            castedParserRules.StartRowNumber = 1;
            castedParserRules.EndRowNumber = 0;
            castedParserRules.HeaderRowNumber = 0;

            //set sample name col
            castedParserRules.SampleInfo.SampleNameColumnNumber = _lineLength + 1;

            //collapse each line
            var semiCollapsedLines =
                recordLineSegments
                .Select(l => string.Join(",", l.ToArray()))
                .ToList();

            //collapse lines into string
            string collapsedLines = String.Join(Environment.NewLine, semiCollapsedLines.ToArray());

            //write all to new file location
            File.WriteAllText(path: destinationPath, contents: collapsedLines);

        }

    }
}
