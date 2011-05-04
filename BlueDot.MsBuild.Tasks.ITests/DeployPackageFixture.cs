using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueDot.MsBuild.Tasks.ITests
{
    /// <summary>
    /// Summary description for DeployPackageFixture
    /// </summary>
    [TestClass]
    public class DeployPackageFixture
    {
        public DeployPackageFixture()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void CanUploadPackage()
        {
            DeployPackage deploy = new DeployPackage();
            deploy.PackageContentDirectory = @"C:\prj\Customer - CTA\Trunk\Construction\Client\Installer\CTA.WOM.Mobile.Setup\PackageContents";
            deploy.PackageDesignUrl = @"http://bdsprjapptst/mnowframework/PackageDesign.svc";           
            deploy.PackageName = "Test Package";
            deploy.PackageVersion = "1.0.6";
            deploy.DeletePreviousPackages = true;
            deploy.DeviceGroupManagementUrl = @"http://bdsprjapptst/mnowframework/DeviceGroupManagement.svc";
            //deploy.DeviceGroup = "ABTestGroup";            
            deploy.Execute();

        }
    }
}
