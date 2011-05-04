using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace BlueDot.MsBuild.Tasks
{
	public class NewGuid : Task
	{
		#region Fields

		private string createdGuid;

		private string format;

		#endregion

		#region Properties

		[Output]
		public string CreatedGuid
		{
			get { return createdGuid; }
			set { createdGuid = value; }
		}

		public string Format
		{
			get { return format; }
			set { format = value; }
		}

		#endregion

		#region public

		public override bool Execute()
		{
			bool success = true;
			try
			{
				createdGuid = Guid.NewGuid().ToString(format).ToUpper();

				Log.LogMessage(MessageImportance.Normal, "Success");
			}
			catch (Exception ex)
			{
				// Log Failure
				Log.LogErrorFromException(ex);
				Log.LogMessage(MessageImportance.High, "Failed");
				success = false;
			}
			return success;
		}

		#endregion
	}
}