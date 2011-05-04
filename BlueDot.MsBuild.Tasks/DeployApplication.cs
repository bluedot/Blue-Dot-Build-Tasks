#region Copyright © 2008, Blue Dot Solutions

// *********************************************************************
// 
// Copyright © 2008, Blue Dot Solutions and/or its affiliates 
// and subsidiaries.  All rights reserved.
// www.bluedotsolutions.com
//       
// Blue Dot Solutions has intellectual property rights relating  
// to technology embodied in this product. In particular, and 
// without limitation, these intellectual property rights may 
// include one or more of U.S. patents or pending patent applications
// in the U.S. and/or other countries.
//      
// This product is distributed under licenses restricting its use, 
// copying, distribution, and decompilation. No part of this product 
// may be reproduced in any form by any means without prior written 
// authorization of Blue Dot Solutions.
//       
// Blue Dot, mNOW!, mNOW! Mobile Framework, mCORE!, mfLY!,
// mCORE! Command Center, mCORE! Communication Agent, mCORE! 
// Communication Server, and mCORE! Integration Engine are trademarks of 
// Blue Dot Solutions.
// 
// *********************************************************************

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;
using BlueDot.MsBuild.Tasks.MCore72Application;
using BlueDot.MsBuild.Tasks.MCore72DeviceGroup;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace BlueDot.MsBuild.Tasks
{
    public class DeployApplication : Task
    {
        #region Fields

        private const string extractArgsTemplate = "/Y /E /L \"{0}\" \"{1}\"";

        #endregion

        #region Properties

        public string ApplicationManagementUrl { get; set; }
        public string DeviceGroup { get; set; }
        public string DeviceGroupManagementUrl { get; set; }
        public string InstallCommandLine { get; set; }
        public string InstallFilePath { get; set; }
        public bool ShouldRemovePreviousVersion { get; set; }
        public string Version { get; set; }

        #endregion

        #region public

        public override bool Execute()
        {
            try
            {
                using (var appClient = GetApplicationManagementService())
                {
                    using (var groupClient = GetDeviceGroupManagementService())
                    {
                        // Create the application install
                        var app = FindOrCreateApplication(appClient);
                        var install = CreateInstall(appClient, app);

                        // assign to a group
                        int? groupId = GetGroupId(groupClient, null);
                        if (groupId.HasValue)
                        {
                            var assignedApps = groupClient.GetApplicationsAssociatedToGroup(groupId.Value);
                            if (assignedApps.SingleOrDefault(p => p.ApplicationId == app.ApplicationId) == null)
                            {
                                groupClient.AssociateApplications(
                                    new[] {app.ApplicationId},
                                    Environment.UserName,
                                    groupId.Value);

                                LogMessage(MessageImportance.Low,
                                           String.Format(
                                               "Associated application to group '{0}' for first time", DeviceGroup));
                            }

                            // Make this install active
                            groupClient.UpdateInstallForGroupApplication(
                                new DeviceGroupInstallUpdate
                                    {
                                        ApplicationId = app.ApplicationId,
                                        DeployToDeviceDate = DateTime.UtcNow,
                                        DeviceGroupId = groupId.Value,
                                        InstallId = install.InstallId,
                                        InstallOnDeviceDate = DateTime.UtcNow,
                                        IsActiveInstall = true,
                                        LastUpdatedByUserName = Environment.UserName
                                    });

                            LogMessage(MessageImportance.Low,
                                       "Install has been set as active install with immediate download and install scheduled");
                        }
                        else
                        {
                            LogMessage(MessageImportance.Normal,
                                       String.Format("Unable to find group named '{0}'. App was not assigned to group",
                                                     DeviceGroup));
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage(MessageImportance.High, "Failed");
                LogMessage(MessageImportance.High, ex.ToString());
                return false;
            }
        }

        #endregion

        #region private

        private static CustomBinding CreateDefaultBinding()
        {
            var binding = new CustomBinding();
            binding.Elements.Add(new HttpTransportBindingElement()
                {
                    MaxReceivedMessageSize = 1073741824
                });
            return binding;
        }

        private InstallHeader CreateInstall(ApplicationManagementClient proxy, Application app)
        {
            using (var fileStream = new FileStream(InstallFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var install = new NewInstall
                                  {
                                      ApplicationId = app.ApplicationId,
                                      CreatedByUserName = Environment.UserName,
                                      Version = Version,
                                      InstallFileName = Path.GetFileName(InstallFilePath),
                                      InstallCommandLine = InstallCommandLine,
                                      ShouldRemovePreviousVersion = ShouldRemovePreviousVersion
                                  };
                var i = proxy.CreateInstall(install, fileStream);

                LogMessage(MessageImportance.Low, String.Format("Created new install id = {0}", i.InstallId));

                return i;
            }
        }

        private Application FindOrCreateApplication(ApplicationManagementClient proxy)
        {
            var applications = GetAllApplications(proxy);

            if (InstallFilePath.ToUpperInvariant().EndsWith(".MSI"))
            {
                var info = SimpleMsiReader.GetMsiInfo(InstallFilePath);
                Version = info.ProductVersion;
                if (string.IsNullOrEmpty(InstallCommandLine))
                {
                    InstallCommandLine = String.Format("/i \"{0}\" /qn", Path.GetFileName(InstallFilePath));
                }
                var app = applications.SingleOrDefault(p => p.ProductCode == info.ProductCode);
                if (app == null)
                {
                    app = proxy.CreateApplication(
                        new NewApplication
                            {
                                ApplicationFolderId = 1,
                                ApplicationFormalName = info.ProductName,
                                ProductCode = info.ProductCode,
                                CreatedByUserName = Environment.UserName,
                                CustomDeviceHandlerModuleId = 4
                            });
                    
                    LogMessage(MessageImportance.Low,
                               String.Format("Created new application '{0} ({1})' already exists", info.ProductName,
                                             info.ProductCode));
                }
                else
                {
                    LogMessage(MessageImportance.Low,
                               String.Format("Application '{0} ({1})' already exists", info.ProductName,
                                             info.ProductCode));
                }
                return app;
            }

            if (InstallFilePath.ToUpperInvariant().EndsWith(".CAB"))
            {
                var name = GetApplicationNameFromCabFile(InstallFilePath);

                var app = applications.SingleOrDefault(p => p.ApplicationFormalName == name);
                if (app == null)
                {
                    app = proxy.CreateApplication(
                        new NewApplication
                            {
                                ApplicationFolderId = 1,
                                ApplicationFormalName = name,
                                CreatedByUserName = Environment.UserName,
                                CustomDeviceHandlerModuleId = 3
                            });

                    LogMessage(MessageImportance.Low, String.Format("Created new application '{0}'", name));
                }
                else
                {
                    LogMessage(MessageImportance.Low, String.Format("Application '{0}' already exists", name));
                }
                return app;
            }

            throw new NotImplementedException("Only CAB and MSI files are supported");
        }

        private static List<Application> GetAllApplications(ApplicationManagementClient proxy)
        {
            var folders = GetSubFolders(proxy, null);
            var applications = new List<Application>();
            foreach (var folder in folders)
            {
                var apps = proxy.GetApplications(folder.ApplicationFolderId).ToList();
                applications.AddRange(apps);
            }
            return applications;
        }

        private ApplicationManagementClient GetApplicationManagementService()
        {
            var client = new ApplicationManagementClient(
                CreateDefaultBinding(),
                new EndpointAddress(ApplicationManagementUrl));

            return client;
        }

        private static string GetApplicationNameFromCabFile(string filePath)
        {
            if (File.Exists(filePath) == false)
                throw new ArgumentException("filePath does not exist");

            var tempPath = Path.GetTempPath();
            tempPath = tempPath + "_BDS" + Path.DirectorySeparatorChar + Guid.NewGuid() +
                       Path.DirectorySeparatorChar;
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);
            Directory.CreateDirectory(tempPath);

            try
            {
                string args = String.Format(extractArgsTemplate, tempPath, filePath);
                using (var proc = Process.Start("extrac32.exe", args))
                {
                    if (proc == null)
                        throw new ApplicationException("Unable to start extrac32");

                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                        throw new ApplicationException("extrac32 failed with exit code " + proc.ExitCode);

                    var setupFile = tempPath + "_setup.xml";
                    if (File.Exists(setupFile) == false)
                        throw new ApplicationException("Unable to find _setup.xml in CAB File");

                    var doc = new XmlDocument();
                    doc.Load(setupFile);
                    return doc.SelectSingleNode(
                        "/wap-provisioningdoc/characteristic[@type='Install']/parm[@name='AppName']").Attributes[
                        "value"].Value;
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempPath))
                        Directory.Delete(tempPath, true);
                }
                catch
                {
                }
            }
        }

        private DeviceGroupManagementClient GetDeviceGroupManagementService()
        {
            var proxy = new DeviceGroupManagementClient(
                CreateDefaultBinding(),
                new EndpointAddress(DeviceGroupManagementUrl));
            return proxy;
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
                    if (groups[i].Name == DeviceGroup)
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

        private static List<ApplicationFolder> GetSubFolders(ApplicationManagementClient proxy, int? parentId)
        {
            var folders = proxy.GetApplicationFolders(parentId).ToList();
            foreach (var folder in folders.ToArray())
            {
                var subFolders = GetSubFolders(proxy, folder.ApplicationFolderId);
                folders.AddRange(subFolders);
            }
            return folders;
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

        #endregion
    }
}