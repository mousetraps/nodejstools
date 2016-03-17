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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NodejsTools.Npm.SPI {
    internal class NpmCommander : AbstractNpmLogSource, INpmCommander {
        private NpmController _npmController;
        private NpmCommand _command;
        private bool _disposed;

        public NpmCommander(NpmController controller) {
            _npmController = controller;
            CommandStarted += _npmController.LogCommandStarted;
            OutputLogged += _npmController.LogOutput;
            ErrorLogged += _npmController.LogError;
            ExceptionLogged += _npmController.LogException;
            CommandCompleted += _npmController.LogCommandCompleted;
        }

        public void Dispose() {
            if (!_disposed) {
                _disposed = true;
                CommandStarted -= _npmController.LogCommandStarted;
                OutputLogged -= _npmController.LogOutput;
                ErrorLogged -= _npmController.LogError;
                ExceptionLogged -= _npmController.LogException;
                CommandCompleted -= _npmController.LogCommandCompleted;
            }
        }

        public void CancelCurrentCommand() {
            if (null != _command) {
                _command.CancelCurrentTask();
            }
        }

        private void command_CommandStarted(object sender, EventArgs e) {
            OnCommandStarted();
        }

        private void command_ExceptionLogged(object sender, NpmExceptionEventArgs e) {
            OnExceptionLogged(e.Exception);
        }

        private void command_ErrorLogged(object sender, NpmLogEventArgs e) {
            OnErrorLogged(e.LogText);
        }

        private void command_OutputLogged(object sender, NpmLogEventArgs e) {
            OnOutputLogged(e.LogText);
        }

        private void command_CommandCompleted(object sender, NpmCommandCompletedEventArgs e) {
            OnCommandCompleted(e.Arguments, e.WithErrors, e.Cancelled);
        }

        private void RegisterLogEvents(NpmCommand command) {
            command.CommandStarted += command_CommandStarted;
            command.OutputLogged += command_OutputLogged;
            command.CommandCompleted += command_CommandCompleted;

            command.ErrorLogged += command_ErrorLogged;
            command.ExceptionLogged += command_ExceptionLogged;
        }

        private void UnregisterLogEvents(NpmCommand command) {
            command.CommandStarted -= command_CommandStarted;
            command.OutputLogged -= command_OutputLogged;
            command.CommandCompleted -= command_CommandCompleted;

            command.ErrorLogged -= command_ErrorLogged;
            command.ExceptionLogged -= command_ExceptionLogged;
        }

        private async Task<bool> DoCommandExecute(bool refreshNpmController) {
            try {
                RegisterLogEvents(_command);
                bool success = await _command.ExecuteAsync();
                UnregisterLogEvents(_command);
                if (refreshNpmController) {
                    _npmController.Refresh();
                }
                return success;
            } catch (Exception e) {
                OnOutputLogged(e.ToString());
            }
            return false;

        }

        public async Task<bool> Install() {
            _command = new NpmInstallCommand(
                _npmController.FullPathToRootPackageDirectory,
                _npmController.PathToNpm);
            return await DoCommandExecute(true);
        }

        private async Task<bool> InstallPackageByVersionAsync(
            string packageName,
            string versionRange,
            DependencyType type,
            bool global,
            bool saveToPackageJson) {
            _command = new NpmInstallCommand(
                _npmController.FullPathToRootPackageDirectory,
                packageName,
                versionRange,
                type,
                global,
                saveToPackageJson,
                _npmController.PathToNpm);
            return await DoCommandExecute(true);
        }

        public async Task<bool> InstallPackageByVersionAsync(
            string packageName,
            string versionRange,
            DependencyType type,
            bool saveToPackageJson) {
            return await InstallPackageByVersionAsync(packageName, versionRange, type, false, saveToPackageJson);
        }

        public async Task<bool> InstallGlobalPackageByVersionAsync(string packageName, string versionRange) {
            return await InstallPackageByVersionAsync(packageName, versionRange, DependencyType.Standard, true, false);
        }

        private DependencyType GetDependencyType(string packageName) {
            var type = DependencyType.Standard;
            var root = _npmController.RootPackage;
            if (null != root) {
                var match = root.Modules[packageName];
                if (null != match) {
                    if (match.IsDevDependency) {
                        type = DependencyType.Development;
                    } else if (match.IsOptionalDependency) {
                        type = DependencyType.Optional;
                    }
                }
            }
            return type;
        }

        private async Task<bool> UninstallPackageAsync(string packageName, bool global) {
            _command = new NpmUninstallCommand(
                _npmController.FullPathToRootPackageDirectory,
                packageName,
                GetDependencyType(packageName),
                global,
                _npmController.PathToNpm);
            return await DoCommandExecute(true);
        }

        public async Task<bool> UninstallPackageAsync(string packageName) {
            return await UninstallPackageAsync(packageName, false);
        }

        public async Task<bool> UninstallGlobalPackageAsync(string packageName) {
            return await UninstallPackageAsync(packageName, true);
        }

        public async Task<IPackageCatalog> GetCatalogAsync(bool forceDownload, IProgress<string> progress) {
            _command = new NpmGetCatalogCommand(
                _npmController.FullPathToRootPackageDirectory,
                _npmController.CachePath,
                forceDownload,
                pathToNpm:_npmController.PathToNpm,
                progress: progress);
            await DoCommandExecute(false);
            return (_command as NpmGetCatalogCommand).Catalog;
        }

        public async Task<bool> UpdatePackagesAsync() {
            return await UpdatePackagesAsync(new List<IPackage>());
        }

        private async Task<bool> UpdatePackagesAsync(IEnumerable<IPackage> packages, bool global) {
            _command = new NpmUpdateCommand(
                _npmController.FullPathToRootPackageDirectory,
                packages,
                global,
                _npmController.PathToNpm);
            return await DoCommandExecute(true);
        }

        public async Task<bool> UpdatePackagesAsync(IEnumerable<IPackage> packages) {
            return await UpdatePackagesAsync(packages, false);
        }

        public async Task<bool> UpdateGlobalPackagesAsync(IEnumerable<IPackage> packages) {
            return await UpdatePackagesAsync(packages, true);
        }

        public async Task<bool> ExecuteNpmCommandAsync(string arguments) {
            _command = new GenericNpmCommand(
                _npmController.FullPathToRootPackageDirectory,
                arguments,
                _npmController.PathToNpm);
            return await DoCommandExecute(true);
        }
    }
}