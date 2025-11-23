/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Meta.XR.Editor
{
    [InitializeOnLoad]
    internal static class PassthroughCameraAccessAndroidManifestRules
    {
        private const string _permission = OVRPermissionsRequester.PassthroughCameraAccessPermission;
        private static bool _hasPermissionInAndroidManifestCached;

        static PassthroughCameraAccessAndroidManifestRules()
        {
            OVRProjectSetup.AddTask(OVRProjectSetup.TaskGroup.Features, static _ =>
                {
                    var config = OVRProjectConfig.CachedProjectConfig;
                    if (config == null)
                    {
                        return false;
                    }
                    if (Object.FindAnyObjectByType<PassthroughCameraAccess>(FindObjectsInactive.Include) == null)
                    {
                        return true;
                    }
                    return config.isPassthroughCameraAccessEnabled && config.insightPassthroughSupport != OVRProjectConfig.FeatureSupport.None && HasPermissionInAndroidManifest();
                }, BuildTargetGroup.Android, OVRProjectSetup.TaskTags.RegenerateAndroidManifest, static _ =>
                {
                    var config = OVRProjectConfig.CachedProjectConfig;
                    if (config != null)
                    {
                        if (config.insightPassthroughSupport == OVRProjectConfig.FeatureSupport.None)
                        {
                            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Supported;
                        }
                        config.isPassthroughCameraAccessEnabled = true;
                        EditorUtility.SetDirty(config);
                    }
                }, OVRProjectSetup.TaskLevel.Required, null,
                "Passthrough Camera Access requires:\n" +
                $"1. '{_permission}' permission in AndroidManifest.xml.\n" +
                "2. 'Passthrough Support' set to 'Supported' or 'Required'.");
        }

        private static bool HasPermissionInAndroidManifest()
        {
            if (!_hasPermissionInAndroidManifestCached)
            {
                const string path = "Assets/Plugins/Android/AndroidManifest.xml";
                _hasPermissionInAndroidManifestCached = File.Exists(path) && File.ReadAllText(path).Contains(_permission);
            }
            return _hasPermissionInAndroidManifestCached;
        }
    }
}
