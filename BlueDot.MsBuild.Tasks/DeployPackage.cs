using System;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml.Serialization;
using BlueDot.MsBuild.Tasks.MCoreDeviceGroup;
using BlueDot.MsBuild.Tasks.MCorePackage;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace BlueDot.MsBuild.Tasks
{
    public class DeployPackage : Task
    {

        public string PackageDesignUrl { get; set; }

        public string PackageContentDirectory { get; set; }

        public string PackageName { get; set; }

        public string PackageVersion { get; set; }

        public bool DeletePreviousPackages { get; set; }

        public string DeviceGroup { get; set; }

        public string DeviceGroupManagementUrl { get; set; }


        public override bool Execute()
        {
            bool success = true;
            try
            {
                int newPackageId = 0;
                using (var destinationStream = new MemoryStream())
                {
                    using (var outputStream = new ZipOutputStream(destinationStream))
                    {                        
                        LoadPackage(outputStream);
                        byte[] bytes = destinationStream.ToArray();
                        newPackageId = UploadPackage(bytes);
                        ExportPackage(newPackageId, bytes);
                    }
                }
                AddPackageToGroup(newPackageId);
            }
            catch (Exception ex)
            {
                LogMessage(MessageImportance.High, ex.ToString());
                success = false;
            }
            return success;
        }

        private void ExportPackage(int newPackageId, byte[] bytes)
        {
            using (var service = GetPackageDesignService())
            {   
                DeploymentPackageHeader packageHeader = GetPackageHeader(service, newPackageId);

                using (var destinationStream = new MemoryStream())
                {
                    using (var outputStream = new ZipOutputStream(destinationStream))
                    {
                        outputStream.SetLevel(5); // 0 - store only to 9 - means best compression

                        var entry = new ZipEntry("PackageHeader.xml");
                        outputStream.PutNextEntry(entry);
                        byte[] headerArray = GetHeaderByteArray(packageHeader);
                        outputStream.Write(headerArray, 0, headerArray.Length);

                        entry = new ZipEntry("PackageContents.zip");
                        outputStream.PutNextEntry(entry);
                        outputStream.Write(bytes, 0, bytes.Length);

                        outputStream.Finish();

                        string path = Path.Combine(
                            this.PackageContentDirectory,
                            string.Format("{0}_{1}.zip",
                                this.PackageName, this.PackageVersion));

                        using (var bw = new BinaryWriter(File.OpenWrite(path)))
                        {
                            bw.Write(destinationStream.ToArray());
                        }
                    }
                }
            }
        }

        private DeploymentPackageHeader GetPackageHeader(
            PackageDesignClient service, int newPackageId)
        {
            DeploymentPackageMetadata[] response = service.GetPackageList();
            
            foreach (var header in response)
            {
                if (header.Id == newPackageId)
                {
                    return TranslateHeader(header);
                }
            }
            return null;
        }

        private DeploymentPackageHeader TranslateHeader(
            DeploymentPackageMetadata value)
        {
            DeploymentPackageHeader package = new DeploymentPackageHeader();
            package.Id = value.Id;
            package.Name = value.Name;
            package.Version = value.Version;
            package.TypeId = value.TypeId;
            package.TypeName = value.Type;
            package.CreatedBy = value.CreatedBy;
            package.CreatedWhen = value.CreatedWhen;
            package.LastUpdatedBy = value.LastUpdatedBy;
            package.LastUpdatedWhen = value.LastUpdatedWhen;

            return package;            
        }

        private byte[] GetHeaderByteArray(DeploymentPackageHeader header)
        {
            using (MemoryStream headerStream = new MemoryStream())
            {
                // Create the header file
                XmlSerializer serializer = new XmlSerializer(typeof(DeploymentPackageHeader));
                serializer.Serialize(headerStream, header);
                headerStream.Flush();
                return headerStream.ToArray();
            }
        }

        public void AddPackageToGroup(int newPackageId)
        {
            if (!string.IsNullOrEmpty(this.DeviceGroup))
            {
                using (DeviceGroupManagementClient service = this.GetDeviceGroupManagementService())
                {
                    int? groupId = GetGroupId(service, null);

                    if (groupId.HasValue)
                    {                        
                        DeviceGroupAssociationAddition[] addList = new DeviceGroupAssociationAddition[1] {
                        new DeviceGroupAssociationAddition()
                        {
                            DeploymentPackageId = newPackageId, 
                            DeviceGroupId = groupId.Value, 
                            DeploymentDate = DateTime.Now, 
                            CreatedByUserName = "bds\buildrun", 
                            CreatedWhen = DateTime.Now, 
                            InstallationDate = DateTime.Now, 
                            RemovalDate = DateTime.Now.AddYears(100)
                        }};
                        try
                        {
                            service.AssociateDeploymentPackages(addList);
                            LogMessage(MessageImportance.Normal, 
                                string.Format("Associated Package {0} to device group {1}", newPackageId, this.DeviceGroup));
                        }
                        catch (Exception ex)
                        {
                            LogMessage(MessageImportance.High, "Error Associating Package {0} to Group {1}.");
                            LogMessage(MessageImportance.High, ex.ToString());
                        }                        
                    }
                }
            }
        }

        private int? GetGroupId(DeviceGroupManagementClient service, int? groupId)
        {
            int? foundGroupId = null;
            try
            {
                DeviceGroup[] groups = service.GetChildGroups(groupId);
                
                for (int i = 0; i < groups.Length; i++)
                {
                    if (groups[i].Name == "Authorized Devices")
                    {
                        foundGroupId = GetGroupId(service, groups[i].Id);
                        break;
                    }
                    if (groups[i].Name == this.DeviceGroup)
                    {
                        foundGroupId = groups[i].Id;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage(MessageImportance.High, "Error Retrieving Groups.");
                LogMessage(MessageImportance.High, ex.ToString());

            }
            return foundGroupId;
        }


        private int UploadPackage(byte[] bytes)
        {
            int newPackageId = 0;

            using (var service = GetPackageDesignService())
            {
                DeletePackages(service);

                var packageToAdd = new DeploymentPackageAddition
                {
                    Name = this.PackageName,
                    HandlerId = 2,
                    Version = this.PackageVersion,
                    CreatedWhen = DateTime.Now,
                    CreatedBy = Environment.UserName,
                    Content = bytes
                };

                LogMessage(MessageImportance.Normal, "Submitting Package to mCORE Communications Server...");

                newPackageId = service.AddPackage(packageToAdd);

                LogMessage(MessageImportance.Normal, string.Format("Successfully submitted Package.  Package Id={0}.", newPackageId));

            }
            return newPackageId;
        }

        private void DeletePackages(PackageDesignClient service)
        {
            if (this.DeletePreviousPackages)
            {
                LogMessage(MessageImportance.Normal, string.Format("Deleting Previous {0} Packages.", this.PackageName));
                var packages = service.GetPackageList();
                foreach (var package in packages)
                {
                    if (package.Name == this.PackageName)
                    {
                        service.RemovePackage(package.Id);
                        LogMessage(MessageImportance.Normal, string.Format("Successfully Removed Package.  Package Id={0}.", package.Id));
                    }
                }
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

        private void LoadPackage(ZipOutputStream outputStream)
        {
            outputStream.SetLevel(5); // 0 - store only to 9 - means best compression

            LogMessage(MessageImportance.Normal, "Scanning PackageContents folder...");
            DirectoryInfo dir = new DirectoryInfo(PackageContentDirectory);
            FileInfo[] contentFiles = dir.GetFiles();
            foreach (FileInfo contentFile in contentFiles)
            {
                using (FileStream stream = new FileStream(contentFile.FullName, FileMode.Open))
                {
                    LogMessage(MessageImportance.Normal, string.Format("Adding {0} to Package...", contentFile.Name));
                    ZipEntry entry = new ZipEntry(contentFile.Name);
                    byte[] buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, (int)stream.Length);
                    outputStream.PutNextEntry(entry);
                    outputStream.Write(buffer, 0, buffer.Length);
                }
            }
            outputStream.Finish();
        }

        private PackageDesignClient GetPackageDesignService()
        {            
            PackageDesignClient service = new PackageDesignClient(
                CreateDefaultBinding(),
                new EndpointAddress(this.PackageDesignUrl));
            return service;
        }

        private DeviceGroupManagementClient GetDeviceGroupManagementService()
        {
            DeviceGroupManagementClient service = new DeviceGroupManagementClient(
                CreateDefaultBinding(),
                new EndpointAddress(this.DeviceGroupManagementUrl));
            return service;
        }

        public static System.ServiceModel.Channels.CustomBinding CreateDefaultBinding()
        {
            CustomBinding binding = new CustomBinding();
            binding.Elements.Add(new HttpTransportBindingElement());             
            return binding;
        }
    }

    public class DeploymentPackageHeader
    {
        #region Fields

        private String _createdBy;
        private DateTime _createdWhen;
        private String _description;
        private Int32 _id;
        private String _lastUpdatedBy;
        private DateTime _lastUpdatedWhen;
        private String _name;
        private Int32 _typeId;
        private String _typeName;
        private String _version;

        #endregion

        #region Properties

        public String CreatedBy
        {
            get { return _createdBy; }
            set { _createdBy = value; }
        }

        public DateTime CreatedWhen
        {
            get { return _createdWhen; }
            set { _createdWhen = value; }
        }

        public String Description
        {
            get { return _description; }
            set { _description = value; }
        }


        public Int32 Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public String LastUpdatedBy
        {
            get { return _lastUpdatedBy; }
            set { _lastUpdatedBy = value; }
        }

        public DateTime LastUpdatedWhen
        {
            get { return _lastUpdatedWhen; }
            set { _lastUpdatedWhen = value; }
        }

        public String Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public Int32 TypeId
        {
            get { return _typeId; }
            set { _typeId = value; }
        }

        public String TypeName
        {
            get { return _typeName; }
            set { _typeName = value; }
        }

        public String Version
        {
            get { return _version; }
            set { _version = value; }
        }

        #endregion

        #region public

        public override bool Equals(object obj)
        {
            DeploymentPackageHeader pkg = (DeploymentPackageHeader)obj;
            if (pkg.Id == _id)
                return true;
            else
                return false;
        }

        #endregion
    }
}

