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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueDot.MsBuild.Tasks.ITests
{
    [TestClass]
    public class DeployApplicationFixture
    {
        #region Tests

        [TestMethod]
        public void CanDeployCabFileApp()
        {
            var task = new DeployApplication
                           {
                               ApplicationManagementUrl =
                                   "http://localhost/mnowcommunicationservice/applicationmanagement.svc",
                               InstallFilePath = @"C:\Temp\AmeyMobile_2.0.11805.cab",
                               DeviceGroup = "Test",
                               DeviceGroupManagementUrl =
                                   "http://localhost/mnowcommunicationservice/devicegroupmanagement.svc",
                               Version = "2.0.11805"
                           };

            var result = task.Execute();

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CanDeployMsiFileApp()
        {
            var task = new DeployApplication
                           {
                               ApplicationManagementUrl =
                                   "http://localhost/mnowcommunicationservice/applicationmanagement.svc",
                               InstallFilePath = @"C:\Temp\mCORECommandCenter_7.2.12524.msi",
                               DeviceGroup = "Test",
                               DeviceGroupManagementUrl =
                                   "http://localhost/mnowcommunicationservice/devicegroupmanagement.svc",
                               ShouldRemovePreviousVersion = true,
                           };

            var result = task.Execute();

            Assert.IsTrue(result);
        }

        #endregion
    }
}