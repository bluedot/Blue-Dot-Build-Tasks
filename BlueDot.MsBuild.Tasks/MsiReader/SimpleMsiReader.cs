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
using System.Runtime.InteropServices;
using WindowsInstaller;

namespace BlueDot.MsBuild.Tasks
{
    public static class SimpleMsiReader
    {
        #region public

        public static MsiInfo GetMsiInfo(string filename)
        {
            // Create an Installer instance  
            Type classType = Type.GetTypeFromProgID("WindowsInstaller.Installer");
            var installer = Activator.CreateInstance(classType) as Installer;

            if (installer == null)
                throw new Exception("Unable to create instance of COM Object WindowsInstaller.Installer");

            // Open the msi file for reading  
            // 0 - Read, 1 - Read/Write  
            Database database = installer.OpenDatabase(filename, 0);

            var info = new MsiInfo
                           {
                               Manufacturer = GetProperty(database, "Manufacturer"),
                               ProductName = GetProperty(database, "ProductName"),
                               ProductCode = GetProperty(database, "ProductCode"),
                               ProductVersion = GetProperty(database, "ProductVersion"),
                           };

            Marshal.FinalReleaseComObject(database);

            return info;
        }

        #endregion

        #region private

        private static string GetProperty(Database database, string property)
        {
            string sql = String.Format("SELECT Value FROM Property WHERE Property='{0}'", property);
            var view = database.OpenView(sql);
            view.Execute(null);

            string value = String.Empty;

            // Read in the fetched record  
            Record record = view.Fetch();
            if (record != null)
            {
                value = record.get_StringData(1);
                Marshal.FinalReleaseComObject(record);
            }

            view.Close();
            Marshal.FinalReleaseComObject(view);

            return value;
        }

        #endregion
    }
}