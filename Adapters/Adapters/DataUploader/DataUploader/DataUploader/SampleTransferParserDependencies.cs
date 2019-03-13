using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace DataUploader
{

    //COPIED SAMPLE TRANSFER DATA CONTRACT CLASSES INTO PROJECT
    //SINCE NOT IN SEPERATE PROJECT, EXPOSED AS DLL OR NUGET PACKAGE 

    /// <summary>
    /// /
    /// </summary>
    public interface IParserRules
    {
        /// <summary>
        /// 
        /// </summary>
        string FilePath { get; set; }

        /// <summary>
        /// 
        /// </summary>
        int HeaderRowNumber { get; set; }

        /// <summary>
        /// 
        /// </summary>
        int StartRowNumber { get; set; }
        /// <summary>
        /// 
        /// </summary>
        int EndRowNumber { get; set; }

    }

    /// <summary>
    /// 
    /// </summary>
    public interface ISampleInstanceDataSetParseRules : IParserRules
    {
        ContainerInfo ContainerInfo { get; set; }
        SampleInfo SampleInfo { get; set; }

    }

    /// <summary>
    /// 
    /// </summary>
    [DataContract]
    public class Column
    {
        // column number (starting with 1)
        [DisplayName("Value Column")]
        [DataMember]
        [Display]
        public int Number { get; set; }

        // name to use (instead of what's listed)
        [DisplayName("Value Name('value' if only single dataset value)")]
        [DataMember]
        [Display]
        public string Name { get; set; }

        // default value if column number <= 0 or column value is empty
        [DisplayName("Default Value")]
        [DataMember]
        [Display]
        public string Value { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    [DataContract]
    public class ContainerParserInfo : IParserRules
    {
        /// <summary>
        /// 
        /// </summary>
        public ContainerParserInfo()
        {
            ContainerInfo = new ContainerParserContainerInfo();
            ContainerAttributeColumns = new List<Column>();
            RowFilters = new List<RowFilter>();
        }
        public string FilePath { get; set; }

        // xls, xlsx, csv, tsv
        [DisplayName("File Format(xls, xlsx, csv, tsv)")]
        [DataMember]
        [Display]
        public string FileFormat { get; set; }

        // worksheet name for xls/xlsx files
        public string WorksheetName { get; set; }

        // header row number
        [DisplayName("Header Row")]
        [DataMember]
        [Display]
        public int HeaderRowNumber { get; set; }

        // start row number
        [DisplayName("Start Row")]
        [DataMember]
        [Display]
        public int StartRowNumber { get; set; }

        // end row number (optional); set to '0' if all rows should be processed
        [DisplayName("End Row (enter 0 if all should be processed)")]
        [DataMember]
        [Display]
        public int EndRowNumber { get; set; }

        // optional ExperimentId to create ExperimentContainer and ExperimentWell
        [DisplayName("Experiment Id")]
        [DataMember]
        [Display]
        public int ExperimentId { get; set; }

        /// Include attribute whose value is empty in the result (false by default)
        public bool IncludeEmptyAttributeValue { get; set; }

        // ContainerInfo
        [DisplayName("Container Parser Info")]
        [DataMember]
        [Display]
        public ContainerParserContainerInfo ContainerInfo { get; set; }

        // Specify the attribute each column maps to
        [XmlArrayItemAttribute("ContainerAttributeColumn", typeof(Column))]
        public List<Column> ContainerAttributeColumns;

        // optional: rules to filter records
        [XmlArrayItemAttribute("RowFilter", typeof(RowFilter))]
        public List<RowFilter> RowFilters;
    }

    /// <summary>
    /// 
    /// </summary>
    [DataContract]
    public class ContainerParserContainerInfo
    {
        // Column to extract Container.Barcode (required to uniquely identify a Container record)
        [DisplayName("Barcode Column")]
        [DataMember]
        [Display]
        public int BarcodeColumnNumber { get; set; }

        // Column to extract container size (# wells) which will be translated to rows & cols
        public int ContainerSizeColumnNumber { get; set; }

        // Column to extract container row size 
        [DisplayName("Rows Count Column")]
        [DataMember]
        [Display]
        public int RowSizeColumnNumber { get; set; }

        // Column to extract container col size
        [DisplayName("Columns Count Column")]
        [DataMember]
        [Display]
        public int ColSizeColumnNumber { get; set; }

        [DisplayName("Container Rows (if 'Rows Count Column' not supplied)")]
        [DataMember]
        [Display]
        public int Rows { get; set; }

        // Container.Cols (required, unless ContainerSizeColumnNumber or ColSizeColumnNumber is provided)
        [DisplayName("Container Columns (if 'Columns Count Column' not supplied)")]
        [DataMember]
        [Display]
        public int Cols { get; set; }

        // Column to extract Container.Type
        [DisplayName("Container Type Column")]
        [DataMember]
        [Display]
        public int ContainerTypeColumnNumber { get; set; }

        // Container.Type (if not extracted from a column)
        public string ContainerType { get; set; }

        // Column to extract Container.SubType
        [DisplayName("Container Subtype Column")]
        [DataMember]
        [Display]
        public int ContainerSubTypeColumnNumber { get; set; }

        // Container.SubType (if not extracted from a column)
        public string ContainerSubType { get; set; }

        // Container.TypeSchemaVersion
        [DisplayName("Schema Version")]
        [DataMember]
        [Display]
        public int ContainerSchemaVersion { get; set; }

        // Specifiy whether a container has wells (e.g. microtiter plate, 2D tube) or not (e.g. rack or other types of carriers)
        [DisplayName("Has Wells Flag")]
        [DataMember]
        [Display]
        public bool HasWells { get; set; }

        // column to extract parent container barcode (optional)
        [DisplayName("Parent Container Column")]
        [DataMember]
        [Display]
        public int ParentBarcodeColumnNumber { get; set; }

        // column to extract parent container row (optional)
        [DisplayName("Parent Row Column")]
        [DataMember]
        [Display]
        public int ParentRowColumnNumber { get; set; }

        // column to extract parent container col (optional)
        [DisplayName("Parent Column Column")]
        [DataMember]
        [Display]
        public int ParentColColumnNumber { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    [DataContract]
    public class SampleTransferParserInfo : IParserRules
    {
        /// <summary>
        /// 
        /// </summary>
        public SampleTransferParserInfo()
        {
            SourceWellInfo = new SampleTransferParserWellInfo();
            DestWellInfo = new SampleTransferParserWellInfo();
        }

        public string FilePath { get; set; }

        // xls, xlsx, csv, tsv, psv
        [DisplayName("File Extension(xls, xlsx, csv, tsv, psv)")]
        [DataMember]
        [Display]
        public string FileFormat { get; set; }

        // worksheet name for xls/xlsx files
        public string WorksheetName { get; set; }

        // header row number
        [DisplayName("Header Row")]
        [DataMember]
        [Display]
        public int HeaderRowNumber { get; set; }

        // start row number
        [DisplayName("Start Row")]
        [DataMember]
        [Display]
        public int StartRowNumber { get; set; }

        // end row number (optional); set to '0' if all rows should be processed
        [DisplayName("End Row(Set to 0 if all should be processed)")]
        [DataMember]
        [Display]
        public int EndRowNumber { get; set; }

        //false by default
        [DisplayName("Copy Container Attributes?")]
        [DataMember]
        [Display]
        public bool CopyContainerAttributes { get; set; }

        //false by default
        [DisplayName("Copy Well Attributes?")]
        [DataMember]
        [Display]
        public bool CopyWellAttributes { get; set; }

        // false by default
        // If set to true, any 2D tube that is in a rack but not in the database will be ignored
        [DisplayName("Ignore Missing Child Containers?")]
        [DataMember]
        [Display]
        public bool IgnoreMissingChildContainer { get; set; }

        // special handling to populate DNA Prep ID in SampleInstance.Keywords (False by default)
        public bool IsDNAPrep { get; set; }

        // Source Container/Well Parser Info
        [DisplayName("Source Well Info")]
        [DataMember]
        [Display]
        public SampleTransferParserWellInfo SourceWellInfo { get; set; }

        // Destination Container/Well Parser Info
        [DisplayName("Destination Well Info")]
        [DataMember]
        [Display]
        public SampleTransferParserWellInfo DestWellInfo { get; set; }

        // optional: rules to filter records
        [XmlArrayItem("RowFilter", typeof(RowFilter))]
        public List<RowFilter> RowFilters;
    }

    /// <summary>
    /// 
    /// </summary>
    [DataContract]
    public class SampleTransferParserWellInfo
    {
        /// <summary>
        /// Column to extract Container.Barcode (required to uniquely identify a Container record)
        /// </summary>
        [DisplayName("Barcode Column Number")]
        [DataMember]
        [Display]
        public int BarcodeColumnNumber { get; set; }

        ///---------------------------------------------------------------------------------------
        /// The following attributes are used to create new Container record
        /// (Applicable to Destination Container Only)
        ///---------------------------------------------------------------------------------------
        /// Column to extract container size (# wells) which will be translated to rows & cols
        public int ContainerSizeColumnNumber { get; set; }

        // Hardcoded Container.Rows (will be used unless ContainerSizeColumnNumber is provided)
        [DisplayName("Hard Coded Rows")]
        [DataMember]
        [Display]
        public int Rows { get; set; }

        // Hardcoded Container.Cols (will be used unless ContainerSizeColumnNumber is provided)
        [DisplayName("Hard Coded Columns")]
        [DataMember]
        [Display]
        public int Cols { get; set; }

        // Column to extract Container.Type
        [DisplayName("Container Type Column")]
        [DataMember]
        [Display]
        public int ContainerTypeColumnNumber { get; set; }

        // Hardcoded Container.Type (if not extracted from ContainerTypeColumnNumber)
        public string ContainerType { get; set; }

        // Column to extract Container.SubType
        [DisplayName("Container Subtype Column")]
        [DataMember]
        [Display]
        public int ContainerSubTypeColumnNumber { get; set; }

        // Harcoded Container.SubType (if not extracted from ContainerSubTypeColumnNumber)
        public string ContainerSubType { get; set; }
        ///---------------------------------------------------------------------------------------

        /// <summary>
        /// Column to extract Well.Row (required, unless Well ID is provided)
        /// </summary>
        [DisplayName("Row Column")]
        [DataMember]
        [Display]
        public int RowColumnNumber { get; set; }

        /// <summary>
        /// Optional: harcoded row (e.g. set to 1 for tube & vial)
        /// </summary>
        [DisplayName("Row (hard coded '1' for tube or vial)")]
        [DataMember]
        [Display]
        public int Row { get; set; }

        /// <summary>
        /// Column to extract Well.Col (required, unless Well ID is provided)
        /// </summary>
        [DisplayName("Column Column")]
        [DataMember]
        [Display]
        public int ColColumnNumber { get; set; }

        /// <summary>
        /// Optional: harcoded col (e.g. set to 1 for tube & vial)
        /// </summary>
        [DisplayName("Column (hard coded '1' for tube or vial)")]
        [DataMember]
        [Display]
        public int Col { get; set; }

        /// <summary>
        /// Column to extract Well ID (e.g. "A1" or "A01")
        /// </summary>
        public int WellIdColumnNumber { get; set; }

        /// <summary>
        /// Column to extract well amount change 
        /// </summary>
        [DisplayName("Amount Transferred Column")]
        [DataMember]
        [Display]
        public int AmountTransferColumnNumber { get; set; }

        /// <summary>
        /// Optional: Column to extract Well.AmountUnit 
        /// </summary>
        [DisplayName("Amount Unit Column")]
        [DataMember]
        [Display]
        public int AmountUnitColumnNumber { get; set; }

        /// <summary>
        /// Optional: Well.AmountUnit (if hardcoded)
        /// </summary>
        public string AmountUnit { get; set; }

        // Optional, to be used for concentration e.g. when uploading dilutions
        public int SampleInstanceAmountColumnNumber { get; set; }

        // Optional
        public int SampleInstanceAmountUnitColumnNumber { get; set; }

        // Optional 
        public string SampleInstanceAmountUnit { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    [DataContract]
    public class RowFilter
    {
        public int ColumnNumber { get; set; }

        // allowed operators: EQ (equal), NE (not equal)
        public string Operator { get; set; }

        public string Value { get; set; }
    }


    public class SampleInstanceDatasetParserInfo : ISampleInstanceDataSetParseRules
    {
        public SampleInstanceDatasetParserInfo()
        {
            SampleInfo = new SampleInfo();
            ContainerInfo = new ContainerInfo();
            DatasetInfo = new DatasetInfo();
            DatasetAttributeColumns = new List<Column>() { new Column() { Name = "Value", Number = 0, Value = "None Specified", } };
            RowFilters = new List<RowFilter>();
            EndRowNumber = 0;    // by default process all rows unless a valid last row number is specified
        }

        public string FilePath { get; set; }

        // xls, xlsx, csv, tsv
        [DisplayName("File Extension (xls, xlsx, csv, tsv)")]
        [DataMember]
        [Display]
        public string FileFormat { get; set; }

        // worksheet name for xls/xlsx files

        public string WorksheetName { get; set; }

        // header row number
        [DisplayName("Header Row Number")]
        [DataMember]
        [Display]
        public int HeaderRowNumber { get; set; }

        // start row number
        [DisplayName("Start Row Number")]
        [DataMember]
        [Display]
        public int StartRowNumber { get; set; }

        // end row number (optional); set to '0' if all rows should be processed
        [DisplayName("End Row ('0' if all rows)")]
        [DataMember]
        [Display]
        public int EndRowNumber { get; set; }

        // optional ExperimentId to create ExperimentSampleInstance
        [DisplayName("Experiment Id ('1' for dummy experiment)")]
        [DataMember]
        [Display]
        public int ExperimentId { get; set; }

        /// Include attribute whose value is empty in the result (false by default)
        public bool IncludeEmptyAttributeValue { get; set; }


        public SampleInfo SampleInfo { get; set; }

        [DisplayName("Container Info")]
        [DataMember]
        [Display]
        public ContainerInfo ContainerInfo { get; set; }

        [DisplayName("Dataset Info")]
        [DataMember]
        [Display]
        public DatasetInfo DatasetInfo { get; set; }


        // DatasetAttributeColumns
        [DisplayName("DataSet Attributes")]
        [DataMember]
        [Display]
        [XmlArrayItemAttribute("DatasetAttributeColumn", typeof(Column))]
        public List<Column> DatasetAttributeColumns { get; set; }

        // optional: rules to filter records
        [XmlArrayItemAttribute("RowFilter", typeof(RowFilter))]
        public List<RowFilter> RowFilters { get; set; }
    }

    public class DatasetInfo
    {
        // Specify which column to extract Dataset.Name from
        public int DatasetNameColumnNumber { get; set; }

        // Dataset.Name can be hardcoded
        [DisplayName("Dataset Name")]
        [DataMember]
        [Display]
        public string DatasetName { get; set; }

        [DisplayName("Dataset Type")]
        [DataMember]
        [Display]
        public string DatasetType { get; set; }

        [DisplayName("Dataset Subtype")]
        [DataMember]
        [Display]
        public string DatasetSubType { get; set; }
    }

    public class ContainerInfo
    {
        /// <summary>
        /// Column to extract Container.Barcode (required to uniquely identify a Container record)
        /// </summary>

        public string Barcode { get; set; }

        /// <summary>
        /// Column to extract Container.Barcode (required to uniquely identify a Container record)
        /// </summary>
        [DisplayName("Barcode Column")]
        [DataMember]
        [Display]
        public int BarcodeColumnNumber { get; set; }

        /// <summary>
        /// Optional: harcoded row (e.g. set to 1 for tube & vial)
        /// </summary>
        [DisplayName("Row (hard coded '1' for tube or vial)")]
        [DataMember]
        [Display]
        public int Row { get; set; }

        /// <summary>
        /// Column to extract Well.Row (required, unless Well ID is provided)
        /// </summary>
        public int RowColumnNumber { get; set; }

        /// <summary>
        /// Optional: harcoded col (e.g. set to 1 for tube & vial)
        /// </summary>
        [DisplayName("Column (hard coded '1' for tube or vial)")]
        [DataMember]
        [Display]
        public int Col { get; set; }

        /// <summary>
        /// Column to extract Well.Col (required, unless Well ID is provided)
        /// </summary>
        public int ColColumnNumber { get; set; }

        /// <summary>
        /// Column to extract Well ID (e.g. "A1" or "A01")
        /// </summary>
        public int WellIdColumnNumber { get; set; }
    }

    public class SampleInfo
    {
        public int SampleNameColumnNumber { get; set; }
        public string SampleType { get; set; }
        public string SampleSubType { get; set; }
    }



}
