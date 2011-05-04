using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace BlueDot.MsBuild.Tasks
{
    /// <summary>
    /// 
    /// </summary>
    public class DeployDatabase : Task
    {
 
        public string ConfigFilePath { get; set; }
        public string ScriptsDirectory { get; set; }

        public override bool Execute()
        {
            bool success = true;

            LogMessage(
                MessageImportance.Normal, 
                string.Format("Loading config at {0}", ConfigFilePath));

            XElement config = XElement.Load(ConfigFilePath);
            
            var props = config.Descendants("PropertyGroup").Elements();

            var connStr = GetConnectionString(props);

            var scripts = config.Descendants("AlterScript");

            IEnumerator<XElement> e = scripts.GetEnumerator();

            while (e.MoveNext())
            {
                string file = Path.Combine(ScriptsDirectory, e.Current.Value);
                string variables = GenerateVariables(props);
                DatabaseCreateResult result = RunScript(connStr, file, variables);
                if (!result.IsSuccessful)
                {
                    success = false;                    
                    break;
                }
            }

            return success;
             
        }

        private string GenerateVariables(IEnumerable<XElement> props)
        {
            IEnumerator<XElement> e = props.GetEnumerator();
            StringBuilder sb = new StringBuilder();
            sb.Append(" -v");
            while (e.MoveNext())
            {
                sb.AppendFormat(@" {0}=""{1}"" ", e.Current.Name.LocalName, e.Current.Value);
            }
            return sb.ToString();
        }

        private SqlConnectionString GetConnectionString(IEnumerable<XElement> props)
        {            
            SqlConnectionString connStr = new SqlConnectionString();
            
            IEnumerator<XElement> enumerator = props.GetEnumerator();

            while (enumerator.MoveNext())
            {
                XElement ele = enumerator.Current;
                if (ele.Name.LocalName == "Server")
                {
                    connStr.Server = ele.Value;
                }
                if (ele.Name.LocalName == "Catalog")
                {
                    connStr.Database = ele.Value;
                }
            }
            return connStr;
        }

        private const string SqlCmd = @"""SqlCmd.exe""";

        public DatabaseCreateResult RunScript(
            SqlConnectionString conString, string file, string variables)
        {
            using (var proc = new Process())
            {
                proc.StartInfo.FileName = SqlCmd;
                if (conString.Authentication == false)
                {
                    proc.StartInfo.Arguments = String.Format(@"-U{0} -P{1}", conString.UserID, conString.Password);
                }
                else
                {
                    proc.StartInfo.Arguments += String.Format(" -b -E ");
                }
                proc.StartInfo.Arguments += variables;

                proc.StartInfo.Arguments += String.Format(@"-S{0} -i""{1}""", conString.Server, file);
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.CreateNoWindow = true;

                LogMessage(
                    MessageImportance.Normal,
                    string.Format("Executing SQLCmd: {0}", proc.StartInfo.Arguments));

                proc.Start();

                var result = new StringBuilder(proc.StandardOutput.ReadToEnd());
                proc.WaitForExit();
                result.Append(proc.StandardOutput.ReadToEnd());

                LogMessage(
                    MessageImportance.Normal,
                    string.Format("Execution Results: {0}", result.ToString()));

                return new DatabaseCreateResult { Output = result.ToString(), IsSuccessful = proc.ExitCode != 1 };
            }
        }

        private void LogMessage(MessageImportance messageImportance, string message)
        {
            try
            {
                if (Log != null)
                {
                    Trace.WriteLine(message);
                    Log.LogMessage(messageImportance, message);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
        }
    }
}
