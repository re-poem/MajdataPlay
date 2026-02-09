using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace MajdataPlay.Editor
{
    public static class iOSPostprocessBuild
    {
        private static readonly string[] RunpathAdditions =
        {
            "@executable_path/Frameworks/bass.framework",
            "@executable_path/Frameworks/bass_fx.framework",
            "@executable_path/Frameworks/bassopus.framework",
        };

        [PostProcessBuild(45)]
        public static void OnPostprocessBuild(BuildTarget buildTarget, string path)
        {
            if (buildTarget != BuildTarget.iOS)
            {
                return;
            }

            UpdateRunpathSearchPaths(path);
            UpdateInfoPlist(path);
        }

        private static void UpdateRunpathSearchPaths(string path)
        {
            string projPath = PBXProject.GetPBXProjectPath(path);
            if (!File.Exists(projPath))
            {
                Debug.LogWarning($"Xcode project not found at {projPath}");
                return;
            }

            var proj = new PBXProject();
            proj.ReadFromString(File.ReadAllText(projPath));


            string mainTarget = proj.GetUnityMainTargetGuid();
            string frameworkTarget = proj.GetUnityFrameworkTargetGuid();


            AddRunpathSearchPaths(proj, mainTarget);
            if (!string.IsNullOrEmpty(frameworkTarget))
            {
                AddRunpathSearchPaths(proj, frameworkTarget);
            }

            File.WriteAllText(projPath, proj.WriteToString());
        }

        private static void AddRunpathSearchPaths(PBXProject proj, string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return;
            }

            string current = proj.GetBuildPropertyForAnyConfig(target, "LD_RUNPATH_SEARCH_PATHS") ?? string.Empty;
            var existing = new HashSet<string>(current.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

            foreach (string runpath in RunpathAdditions)
            {
                if (!existing.Contains(runpath))
                {
                    proj.AddBuildProperty(target, "LD_RUNPATH_SEARCH_PATHS", runpath);
                }
            }
        }

        private static void UpdateInfoPlist(string path)
        {
            string projPath = PBXProject.GetPBXProjectPath(path);
            if (!File.Exists(projPath))
            {
                Debug.LogWarning($"Xcode project not found at {projPath}");
                return;
            }

            var proj = new PBXProject();
            proj.ReadFromString(File.ReadAllText(projPath));


            string mainTarget = proj.GetUnityMainTargetGuid();


            string plistPath = ResolveInfoPlistPath(path, proj, mainTarget);
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"Info.plist not found at {plistPath}");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromString(File.ReadAllText(plistPath));
            var root = plist.root;
            root.SetBoolean("UIFileSharingEnabled", true);
            root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
            root.SetBoolean("UISupportsDocumentBrowser", true);
            File.WriteAllText(plistPath, plist.WriteToString());
        }

        private static string ResolveInfoPlistPath(string buildPath, PBXProject proj, string target)
        {
            string plistPath = proj.GetBuildPropertyForAnyConfig(target, "INFOPLIST_FILE");
            if (string.IsNullOrEmpty(plistPath))
            {
                return Path.Combine(buildPath, "Info.plist");
            }

            plistPath = plistPath.Trim().Trim('"');
            if (plistPath.Contains("$(SRCROOT)"))
            {
                plistPath = plistPath.Replace("$(SRCROOT)", buildPath);
            }

            if (Path.IsPathRooted(plistPath))
            {
                return plistPath;
            }

            return Path.Combine(buildPath, plistPath);
        }
    }
}
