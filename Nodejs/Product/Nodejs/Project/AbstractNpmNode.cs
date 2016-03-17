﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.NodejsTools.Npm;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Imaging;
#endif

namespace Microsoft.NodejsTools.Project {
    internal abstract class AbstractNpmNode : HierarchyNode {
        protected readonly NodejsProjectNode _projectNode;

        protected AbstractNpmNode(NodejsProjectNode root)
            : base(root) {
            _projectNode = root;
            ExcludeNodeFromScc = true;
        }

        #region HierarchyNode implementation

        public override Guid ItemTypeGuid {
            get { return VSConstants.GUID_ItemType_VirtualFolder; }
        }

        public override Guid MenuGroupId {
            get { return Guids.NodejsNpmCmdSet; }
        }

        public override int MenuCommandId {
            get { return PkgCmdId.menuIdNpm; }
        }

        /// <summary>
        /// Disable inline editing of Caption.
        /// </summary>
        public sealed override string GetEditLabel() {
            return null;
        }
#if DEV14_OR_LATER
        protected override bool SupportsIconMonikers {
            get { return true; }
        }

        /// <summary>
        /// Returns the icon to use.
        /// </summary>
        protected override ImageMoniker GetIconMoniker(bool open) {
            return KnownMonikers.Reference;
        }
#else
        public sealed override object GetIconHandle(bool open) {
            //We don't want the icon to become an expanded folder 'OpenReferenceFolder'
            //  Thus we always return 'ReferenceFolder'
            return ProjectMgr.GetIconHandleByName(ProjectNode.ImageName.ReferenceFolder);
        }
#endif

        protected override NodeProperties CreatePropertiesObject() {
            return new NpmNodeProperties(this);
        }
#endregion

        public abstract void ManageNpmModules();

        protected void ReloadHierarchy(HierarchyNode parent, IEnumerable<IPackage> modules) {
            //  We're going to reuse nodes for which matching modules exist in the new set.
            //  The reason for this is that we want to preserve the expansion state of the
            //  hierarchy. If we just bin everything off and recreate it all from scratch
            //  it'll all be in the collapsed state, which will be annoying for users who
            //  have drilled down into the hierarchy
            var recycle = new Dictionary<string, DependencyNode>();
            var remove = new List<HierarchyNode>();
            for (var current = parent.FirstChild; null != current; current = current.NextSibling) {
                var dep = current as DependencyNode;
                if (null == dep) {
                    if (!(current is GlobalModulesNode) && !(current is LocalModulesNode)) {
                        remove.Add(current);
                    }
                    continue;
                }

                if (modules != null && modules.Any(
                    module =>
                        module.Name == dep.Package.Name
                        && module.Version == dep.Package.Version
                        && module.IsBundledDependency == dep.Package.IsBundledDependency
                        && module.IsDevDependency == dep.Package.IsDevDependency
                        && module.IsListedInParentPackageJson == dep.Package.IsListedInParentPackageJson
                        && module.IsMissing == dep.Package.IsMissing
                        && module.IsOptionalDependency == dep.Package.IsOptionalDependency)) {
                    recycle[dep.Package.Name] = dep;
                } else {
                    remove.Add(current);
                }
            }

            foreach (var obsolete in remove) {
                parent.RemoveChild(obsolete);
                ProjectMgr.OnItemDeleted(obsolete);
            }

            if (modules != null) {
                foreach (var package in modules) {
                    DependencyNode child;

                    if (recycle.ContainsKey(package.Name)) {
                        child = recycle[package.Name];
                        child.Package = package;
                    }
                    else {
                        child = new DependencyNode(_projectNode, parent as DependencyNode, package);
                        parent.AddChild(child);
                    }

                    ReloadHierarchy(child, package.Modules);
                    if (ProjectMgr.ParentHierarchy != null) {
                        child.ExpandItem(EXPANDFLAGS.EXPF_CollapseFolder);
                    }
                }
            }
        }
    }
}
