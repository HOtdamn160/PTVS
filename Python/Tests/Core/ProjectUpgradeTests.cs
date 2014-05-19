﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.IO;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class ProjectUpgradeTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void UpgradeCheckToolsVersion() {
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services["SVsQueryEditQuerySave"] = null;
            sp.Services["SVsActivityLog"] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            foreach (var testCase in new[] {
                new { Name = "NoToolsVersion.pyproj", Expected = 1 },
                new { Name = "OldToolsVersion.pyproj", Expected = 1 },
                new { Name = "CorrectToolsVersion.pyproj", Expected = 0 },
                new { Name = "NewerToolsVersion.pyproj", Expected = 0 }
            }) {
                int actual;
                Guid factoryGuid;
                uint flags;
                var hr = upgrade.UpgradeProject_CheckOnly(
                    TestData.GetPath(Path.Combine("TestData", "ProjectUpgrade", testCase.Name)),
                    null,
                    out actual,
                    out factoryGuid,
                    out flags
                );

                Assert.AreEqual(0, hr, string.Format("Wrong HR for {0}", testCase.Name));
                Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for {0}", testCase.Name));
                Assert.AreEqual(typeof(PythonProjectFactory).GUID, factoryGuid);
            }
        }

        [TestMethod, Priority(0)]
        public void UpgradeToolsVersion() {
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services["SVsQueryEditQuerySave"] = null;
            sp.Services["SVsActivityLog"] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            foreach (var testCase in new[] {
                new { Name = "NoToolsVersion.pyproj", Expected = 1 },
                new { Name = "OldToolsVersion.pyproj", Expected = 1 },
                new { Name = "CorrectToolsVersion.pyproj", Expected = 0 },
                new { Name = "NewerToolsVersion.pyproj", Expected = 0 }
            }) {
                int actual;
                Guid factoryGuid;
                string newLocation;

                // Use a copy of the project so we don't interfere with other
                // tests using them.
                var origProject = Path.Combine("TestData", "ProjectUpgrade", testCase.Name);
                var tempProject = Path.Combine(TestData.GetTempPath("ProjectUpgrade"), testCase.Name);
                File.Copy(origProject, tempProject);

                var hr = upgrade.UpgradeProject(
                    tempProject,
                    0u,  // no backups
                    null,
                    out newLocation,
                    null,
                    out actual,
                    out factoryGuid
                );

                Assert.AreEqual(0, hr, string.Format("Wrong HR for {0}", testCase.Name));
                Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for {0}", testCase.Name));
                Assert.AreEqual(tempProject, newLocation, string.Format("Wrong location for {0}", testCase.Name));
                if (testCase.Expected != 0) {
                    Assert.IsTrue(
                        File.ReadAllText(tempProject).Contains("ToolsVersion=\"4.0\""),
                        string.Format("Upgraded {0} did not contain ToolsVersion=\"4.0\"", testCase.Name)
                    );
                } else {
                    Assert.IsTrue(
                        File.ReadAllText(tempProject) == File.ReadAllText(origProject),
                        string.Format("Non-upgraded {0} has different content to original", testCase.Name)
                    );
                }
                Assert.AreEqual(typeof(PythonProjectFactory).GUID, factoryGuid);
            }
        }

        [TestMethod, Priority(0)]
        public void UpgradeCheckUserToolsVersion() {
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services["SVsQueryEditQuerySave"] = null;
            sp.Services["SVsActivityLog"] = new MockActivityLog();
            factory.Site = sp;

            var projectFile = TestData.GetPath(Path.Combine("TestData", "ProjectUpgrade", "CorrectToolsVersion.pyproj"));

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            
            foreach (var testCase in new[] {
#if DEV12_OR_LATER
                new { Name = "12.0", Expected = 0 },
#else
                new { Name = "12.0", Expected = 1 },
#endif
                new { Name = "4.0", Expected = 0 }
            }) {
                int actual;
                int hr;
                Guid factoryGuid;
                uint flags;

                var xml = Microsoft.Build.Construction.ProjectRootElement.Create();
                xml.ToolsVersion = testCase.Name;
                xml.Save(projectFile + ".user");

                try {
                    hr = upgrade.UpgradeProject_CheckOnly(
                        projectFile,
                        null,
                        out actual,
                        out factoryGuid,
                        out flags
                    );
                } finally {
                    File.Delete(projectFile + ".user");
                }

                Assert.AreEqual(0, hr, string.Format("Wrong HR for ToolsVersion={0}", testCase.Name));
                Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for ToolsVersion={0}", testCase.Name));
                Assert.AreEqual(typeof(PythonProjectFactory).GUID, factoryGuid);
            }
        }

#if DEV12_OR_LATER
        [TestMethod, Priority(0)]
        public void WebProjectCompatibility() {
            const int ExpressSkuValue = 500;
            const int ShellSkuValue = 1000;
            const int ProSkuValue = 2000;
            const int PremiumUltimateSkuValue = 3000;
            
            const int VWDExpressSkuValue = 0x0040;
            const int WDExpressSkuValue = 0x8000;
            const int PremiumSubSkuValue = 0x0080;
            const int UltimateSubSkuValue = 0x0188;

            const uint Compatible = (uint)0;
            const uint Incompatible = (uint)__VSPPROJECTUPGRADEVIAFACTORYREPAIRFLAGS.VSPUVF_PROJECT_INCOMPATIBLE;

            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            var shell = new MockVsShell();

            sp.Services["SVsQueryEditQuerySave"] = null;
            sp.Services["SVsActivityLog"] = new MockActivityLog();
            sp.Services["SVsShell"] = shell;
            factory.Site = sp;

            var projectFile = TestData.GetPath(Path.Combine("TestData", "ProjectUpgrade", "WebProjectType.pyproj"));

            var upgrade = (IVsProjectUpgradeViaFactory4)factory;

            foreach (var testCase in new[] {
                new { Name = "Ultimate", Sku1 = PremiumUltimateSkuValue, Sku2 = UltimateSubSkuValue, Expected = Compatible },
                new { Name = "Premium", Sku1 = PremiumUltimateSkuValue, Sku2 = PremiumSubSkuValue, Expected = Compatible },
                new { Name = "Professional", Sku1 = ProSkuValue, Sku2 = 0, Expected = Compatible },
                new { Name = "VWDExpress", Sku1 = ExpressSkuValue, Sku2 = VWDExpressSkuValue, Expected = Compatible },
                new { Name = "WDExpress", Sku1 = ExpressSkuValue, Sku2 = WDExpressSkuValue, Expected = Incompatible },
                new { Name = "Shell", Sku1 = ShellSkuValue, Sku2 = 0, Expected = Incompatible }
            }) {
                uint actual;
                Guid factoryGuid;
                uint flags;

                // Change the SKU for each test case.
                shell.Properties[(int)__VSSPROPID2.VSSPROPID_SKUEdition] = testCase.Sku1;
                shell.Properties[(int)__VSSPROPID2.VSSPROPID_SubSKUEdition] = testCase.Sku2;

                upgrade.UpgradeProject_CheckOnly(
                    projectFile,
                    null,
                    out actual,
                    out factoryGuid,
                    out flags
                );

                Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for {0}", testCase.Name));
            }
        }
#endif
    }
}