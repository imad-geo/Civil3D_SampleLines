using System.Collections.Generic;
using System.IO;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using Autodesk.Civil.DatabaseServices;

[assembly: CommandClass(typeof(Civil3D_SampleLines.Commands))]

namespace Civil3D_SampleLines
{
    public class Commands
    {
        // Prefix used for all Sample Line names sl or pk as u want 
        private const string SampleLinePrefix = "SL_";

    
         // ============================================================
        // ALIGNINFO
        // Displays information about the selected Alignment.
        // ============================================================
        [CommandMethod("ALIGNINFO")]
        public void AlignInfo()
        {
            // Get the active document
            Document doc = Application.DocumentManager.MdiActiveDocument;

            // Get the command line editor
            Editor ed = doc.Editor;

            // Get the drawing database
            Database db = doc.Database;

            // Ask the user to select an Alignment
            PromptEntityOptions peo =
                new PromptEntityOptions("\nSelect Alignment: ");

            peo.SetRejectMessage("\nPlease select a Civil 3D Alignment.");
            peo.AddAllowedClass(typeof(Alignment), true);

            PromptEntityResult per = ed.GetEntity(peo);

            if (per.Status != PromptStatus.OK)
                return;

            // Start a transaction
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Open the selected Alignment
                Alignment align =
                    tr.GetObject(per.ObjectId, OpenMode.ForRead) as Alignment;

                // Display Alignment information
                ed.WriteMessage(
                    $"\nName   : {align.Name}" +
                    $"\nStart  : {align.StartingStation:F2}" +
                    $"\nEnd    : {align.EndingStation:F2}" +
                    $"\nLength : {align.Length:F2}");

                tr.Commit();
            }
        }

        // ============================================================
        // CREATEGROUP
        // Creates a Sample Line Group.
        // Creates one Sample Line at the specified station.
        // ============================================================
        [CommandMethod("CREATEGROUP")]
        public void CreateGroup()
        {
            // Get the active document
            Document doc = Application.DocumentManager.MdiActiveDocument;

            // Get the command line editor
            Editor ed = doc.Editor;

            // Get the drawing database
            Database db = doc.Database;

            // Ask the user to select an Alignment
            PromptEntityOptions peo =
                new PromptEntityOptions("\nSelect Alignment: ");

            peo.SetRejectMessage("\nPlease select a Civil 3D Alignment.");
            peo.AddAllowedClass(typeof(Alignment), true);

            PromptEntityResult per = ed.GetEntity(peo);

            if (per.Status != PromptStatus.OK)
                return;

            // Ask for the station
            PromptDoubleOptions pdo =
                new PromptDoubleOptions("\nEnter station: ");

            pdo.AllowNegative = false;
            pdo.AllowZero = true;

            PromptDoubleResult pdr = ed.GetDouble(pdo);

            if (pdr.Status != PromptStatus.OK)
                return;

            double station = pdr.Value;

            // Start a transaction
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Open the Alignment
                Alignment align =
                    tr.GetObject(per.ObjectId, OpenMode.ForRead) as Alignment;

                // Create the Sample Line Group
                ObjectId groupId =
                    SampleLineGroup.Create(
                        "My First Group",
                        align.ObjectId);

                // Create one Sample Line
                ObjectId sampleLineId =
                    SampleLine.Create(
                        "SL_" + station.ToString("0.00"),
                        groupId,
                        station);

                ed.WriteMessage("\nSample Line created successfully.");

                tr.Commit();
            }
        }

        // ============================================================
        // FINDGROUP
        // Reads a CSV file and displays every valid station.
        // (For now, it does NOT create Sample Lines.)
        // ============================================================
        [CommandMethod("FINDGROUP")]
        public void FindGroup()
        {
            // Get the active document
            Document doc = Application.DocumentManager.MdiActiveDocument;

            // Get the command line editor
            Editor ed = doc.Editor;

            // Get the drawing database
            Database db = doc.Database;

            // Ask the user to select a CSV file
            PromptOpenFileOptions pfo =
                new PromptOpenFileOptions("\nSelect CSV file:");

            pfo.Filter = "CSV Files (*.csv)|*.csv";

            PromptFileNameResult pfr =
                ed.GetFileNameForOpen(pfo);

            if (pfr.Status != PromptStatus.OK)
                return;

            // Store the selected file path
            string filePath = pfr.StringResult;

            // Read every line of the CSV file
            string[] lines = File.ReadAllLines(filePath);

            // Loop through every line
            foreach (string line in lines)
            {
                // Try to convert the text into a number
                if (double.TryParse(line, out double station))
                {
                    ed.WriteMessage($"\nStation = {station}");
                }
                else
                {
                    ed.WriteMessage(
                        $"\nWarning : '{line}' is not a valid station.");
                }
            }
        }
        // =======================================================
        // IMPORTCSV
        // Imports stations from a CSV file and creates Sample Lines.
        // If the Sample Line Group already exists, it is reused.
        // Existing Sample Lines are skipped.
        // =======================================================
        [CommandMethod("IMPORTCSV")]
        public void ImportCsv()
        {
            // Get the active document
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // Ask the user to select an Alignment
            PromptEntityOptions peo =
                new PromptEntityOptions("\nSelect Alignment:");

            peo.SetRejectMessage("\nPlease select a Civil 3D Alignment.");
            peo.AddAllowedClass(typeof(Alignment), true);

            PromptEntityResult per = ed.GetEntity(peo);

            if (per.Status != PromptStatus.OK)
                return;

            // Ask the user to select the CSV file
            PromptOpenFileOptions pfo =
                new PromptOpenFileOptions("\nSelect CSV file:");

            pfo.Filter = "CSV Files (*.csv)|*.csv";

            PromptFileNameResult pfr =
                ed.GetFileNameForOpen(pfo);

            if (pfr.Status != PromptStatus.OK)
                return;

            // Get the complete file path
            string filePath = pfr.StringResult;

            // Use the CSV filename as the Sample Line Group name
            // Example:
            // C:\Data\Road1.csv --> Road1
            string groupName =
                Path.GetFileNameWithoutExtension(filePath);

            // Read all stations from the CSV
            List<double> stations =
                ReadStationsFromCsv(filePath);

            // Start a transaction
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {                 // Open the Alignment
                Alignment align =
                    tr.GetObject(per.ObjectId, OpenMode.ForRead) as Alignment;

                // Find the Sample Line Group or create it
                ObjectId groupId =
                    FindOrCreateSampleLineGroup(
                        align,
                        tr,
                        groupName);

                // Read all existing Sample Line stations
                HashSet<double> existingStations =
                    GetExistingStations(
                        groupId,
                        tr);

                // Create only the missing Sample Lines

                int createdCount =
                CreateSampleLines(
                    stations,
                    groupId,
                    existingStations);

                ed.WriteMessage(
                    $"\nImport complete.");

                ed.WriteMessage(
                    $"\nCSV stations : {stations.Count}");

                ed.WriteMessage(
                    $"\nCreated      : {createdCount}");

                ed.WriteMessage(
                    $"\nSkipped      : {stations.Count - createdCount}");

                tr.Commit();
            }
        }
            
            // ============================================================
            // READ STATIONS FROM CSV
            // ============================================================
        private List<double> ReadStationsFromCsv(string filePath)
        {
            List<double> stations = new List<double>();

            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines)
            {
                if (double.TryParse(line, out double station))
                {
                    stations.Add(station);
                }
            }

            return stations;
        }
        // ============================================================
        // FIND OR CREATE SAMPLE LINE GROUP
        // ============================================================
        private ObjectId FindOrCreateSampleLineGroup(
            Alignment align,
            Transaction tr,
            string groupName)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            ObjectIdCollection groups = align.GetSampleLineGroupIds();

            ed.WriteMessage($"\nNumber of groups: {groups.Count}");

            foreach (ObjectId groupId in groups)
            {
                ed.WriteMessage($"\nObjectId.IsNull = {groupId.IsNull}");

                if (groupId.IsNull)
                {
                    ed.WriteMessage("\nSkipping Null ObjectId.");
                    continue;
                }

                SampleLineGroup group =
                    tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;

                ed.WriteMessage(
                        "\nCreating group...");

                if (group.Name == groupName)
                    return groupId;
            }

            ed.WriteMessage($"\nCreating group: {groupName}");

            return SampleLineGroup.Create(
                groupName,
                align.ObjectId);
        }
        // ============================================================
        // Create only the stations that do not already exist.
        // ============================================================
            private int CreateSampleLines(
            List<double> stations,
            ObjectId groupId,
            HashSet<double> existingStations)
        {
            int createdCount = 0;
            foreach (double station in stations)
            {
                // Skip stations that already exist
                if (existingStations.Contains(station))
                    continue;

                SampleLine.Create(
                    SampleLinePrefix + station.ToString("0.00"),
                    groupId,
                    station);
                createdCount++;
            }
            return createdCount;
        }
        // =======================================================
        // GETEXISTINGSTATIONS
        // Reads all Sample Lines in the group and returns
        // their stations.
        // =======================================================
        private HashSet<double> GetExistingStations(
            ObjectId groupId,
            Transaction tr)
        {
            // Store all existing stations
            HashSet<double> existingStations = new HashSet<double>();

            // Open the Sample Line Group
            SampleLineGroup group =
                tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;

            // Get all Sample Line ObjectIds
            ObjectIdCollection sampleLineIds =
                group.GetSampleLineIds();

            // Read every Sample Line
            foreach (ObjectId sampleLineId in sampleLineIds)
            {
                SampleLine sampleLine =
                    tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;

                // Save its station
                existingStations.Add(sampleLine.Station);
            }

            return existingStations;
        }
    }
}    


