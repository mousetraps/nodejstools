﻿//*********************************************************//
//    Copyright (c) Microsoft. All rights reserved.
//    
//    Apache 2.0 License
//    
//    You may obtain a copy of the License at
//    http://www.apache.org/licenses/LICENSE-2.0
//    
//    Unless required by applicable law or agreed to in writing, software 
//    distributed under the License is distributed on an "AS IS" BASIS, 
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or 
//    implied. See the License for the specific language governing 
//    permissions and limitations under the License.
//
//*********************************************************//

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Microsoft.NodejsTools;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Nodejs;
using TestUtilities.UI;

namespace Microsoft.Nodejs.Tests.UI {
    [TestClass]
    public class AzureProjectTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            NodejsTestData.Deploy();
        }

        private static void CloudProjectTest(string roleType, bool openServiceDefinition) {
            Assert.IsTrue(roleType == "Web" || roleType == "Worker", "Invalid roleType: " + roleType);

            using (var app = new VisualStudioApp())
            using (FileUtils.Backup(TestData.GetPath(@"TestData\CloudProject\CloudProject\ServiceDefinition.csdef"))) {
                app.OpenProject("TestData\\CloudProject.sln", expectedProjects: 3);

                var ccproj = app.Dte.Solution.Projects.Cast<EnvDTE.Project>().FirstOrDefault(p => p.Name == "CloudProject");
                Assert.IsNotNull(ccproj);

                if (openServiceDefinition) {
                    var wnd = ccproj.ProjectItems.Item("ServiceDefinition.csdef").Open();
                    wnd.Activate();
                    app.OnDispose(() => wnd.Close());
                }

                IVsHierarchy hier;
                var sln = app.GetService<IVsSolution>(typeof(SVsSolution));
                ErrorHandler.ThrowOnFailure(sln.GetProjectOfUniqueName(ccproj.FullName, out hier));

                app.ServiceProvider.GetUIThread().Invoke(() =>
                    NodejsProject.UpdateServiceDefinition(
                        hier,
                        roleType,
                        roleType + "Role1",
                        new ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)app.Dte)
                    )
                );

                var doc = new XmlDocument();
                for (int retries = 5; retries > 0; --retries) {
                    try {
                        doc.Load(TestData.GetPath(@"TestData\CloudProject\CloudProject\ServiceDefinition.csdef"));
                        break;
                    } catch (IOException ex) {
                        Console.WriteLine("Exception while reading ServiceDefinition.csdef.{0}{1}", Environment.NewLine, ex);
                    } catch (XmlException) {
                        var copyTo = TestData.GetPath(@"TestData\CloudProject\CloudProject\" + Path.GetRandomFileName());
                        File.Copy(TestData.GetPath(@"TestData\CloudProject\CloudProject\ServiceDefinition.csdef"), copyTo);
                        Console.WriteLine("Copied file to " + copyTo);
                        throw;
                    }
                    Thread.Sleep(100);
                }
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("sd", "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition");
                doc.Save(Console.Out);

                var nav = doc.CreateNavigator();
                if (roleType == "Web") {
                    Assert.IsNotNull(nav.SelectSingleNode(
                        "/sd:ServiceDefinition/sd:WebRole[@name='WebRole1']/sd:Startup/sd:Task[@commandLine='setup_web.cmd > log.txt']",
                        ns
                    ));
                } else if (roleType == "Worker") {
                    Assert.IsNotNull(nav.SelectSingleNode(
                        "/sd:ServiceDefinition/sd:WorkerRole[@name='WorkerRole1']/sd:Startup/sd:Task[@commandLine='setup_worker.cmd > log.txt']",
                        ns
                    ));
                    Assert.IsNotNull(nav.SelectSingleNode(
                        "/sd:ServiceDefinition/sd:WorkerRole[@name='WorkerRole1']/sd:Runtime/sd:EntryPoint/sd:ProgramEntryPoint[@commandLine='node.cmd .\\server.js']",
                        ns
                    ));
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core"), TestCategory("Ignore")]
        [HostType("VSTestHost")]
        public void UpdateWebRoleServiceDefinitionInVS() {
            CloudProjectTest("Web", false);
        }

        [TestMethod, Priority(0), TestCategory("Core"), TestCategory("Ignore")]
        [HostType("VSTestHost")]
        public void UpdateWorkerRoleServiceDefinitionInVS() {
            CloudProjectTest("Worker", false);
        }

        [TestMethod, Priority(0), TestCategory("Core"), TestCategory("Ignore")]
        [HostType("VSTestHost")]
        public void UpdateWebRoleServiceDefinitionInVSDocumentOpen() {
            CloudProjectTest("Web", true);
        }

        [TestMethod, Priority(0), TestCategory("Core"), TestCategory("Ignore")]
        [HostType("VSTestHost")]
        public void UpdateWorkerRoleServiceDefinitionInVSDocumentOpen() {
            CloudProjectTest("Worker", true);
        }

        // TODO: Remove after FileUtils is merged
        static class FileUtils {
            public static IDisposable Backup(string path) {
                var backup = Path.GetTempFileName();
                File.Delete(backup);
                File.Copy(path, backup);
                return new FileRestorer(path, backup);
            }

            private sealed class FileRestorer : IDisposable {
                private readonly string _original, _backup;

                public FileRestorer(string original, string backup) {
                    _original = original;
                    _backup = backup;
                }

                public void Dispose() {
                    for (int retries = 10; retries > 0; --retries) {
                        try {
                            File.Delete(_original);
                            File.Move(_backup, _original);
                            return;
                        } catch (IOException) {
                        } catch (UnauthorizedAccessException) {
                            try {
                                File.SetAttributes(_original, FileAttributes.Normal);
                            } catch (IOException) {
                            } catch (UnauthorizedAccessException) {
                            }
                        }
                        Thread.Sleep(100);
                    }

                    Assert.Fail("Failed to restore {0} from {1}", _original, _backup);
                }
            }
        }
    }
}
