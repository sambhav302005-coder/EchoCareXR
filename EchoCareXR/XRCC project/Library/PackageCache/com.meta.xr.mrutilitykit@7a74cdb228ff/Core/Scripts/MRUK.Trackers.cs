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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Events;

namespace Meta.XR.MRUtilityKit
{
    partial class MRUK
    {
        partial class MRUKSettings
        {
            /// <summary>
            /// The requested configuration of the tracking service.
            /// </summary>
            /// <remarks>
            /// This property represents the requested tracker configuration (which types of trackables to track). It is possible that some
            /// configuration settings may not be satisfied (for example, due to lack of device support). <see cref="MRUK.TrackerConfiguration"/>
            /// represents the true state of the system.
            /// </remarks>
            [field: SerializeField, Tooltip("Settings related to trackables that are detectable in the environment at runtime.")]
            public OVRAnchor.TrackerConfiguration TrackerConfiguration { get; set; }

            /// <summary>
            /// Invoked when a newly detected trackable has been localized.
            /// </summary>
            /// <remarks>
            /// When a new <see cref="OVRAnchor"/> has been detected and localized, a new `GameObject` with a <see cref="MRUKTrackable"/> is created
            /// to represent it. Its transform is set, and then this event is invoked.
            ///
            /// Subscribe to this event to add additional child GameObjects or further customize the behavior.
            ///
            /// <example>
            /// This example shows how to create a MonoBehaviour that instantiates a custom prefab:
            /// <code><![CDATA[
            /// class MyCustomManager : MonoBehaviour
            /// {
            ///     public GameObject Prefab;
            ///
            ///     public void OnTrackableAdded(MRUKTrackable trackable)
            ///     {
            ///         Instantiate(Prefab, trackable.transform);
            ///     }
            /// }
            /// ]]></code>
            /// </example>
            /// </remarks>
            [field: SerializeField, Tooltip("Invoked after a newly detected anchor has been localized.")]
            public UnityEvent<MRUKTrackable> TrackableAdded { get; private set; } = new();

            /// <summary>
            /// Invoked when an existing trackable is no longer detected by the runtime.
            /// </summary>
            /// <remarks>
            /// When an anchor is removed, no action is taken by default. The <see cref="MRUKTrackable"/>, if any, is not destroyed or deactivated.
            /// Subscribe to this event to change this behavior.
            ///
            /// Once this event has been invoked, the <see cref="MRUKTrackable"/>'s anchor (<see cref="MRUKTrackable.Anchor"/>) is no longer valid.
            /// </remarks>
            [field: SerializeField, Tooltip("The event is invoked when an anchor is removed.")]
            public UnityEvent<MRUKTrackable> TrackableRemoved { get; private set; } = new();
        }

        /// <summary>
        /// The current configuration for the tracking service.
        /// </summary>
        /// <remarks>
        /// To request a particular configuration, set the desired values in <see cref="MRUKSettings.TrackerConfiguration"/>.
        /// This property represents the true state of the system.
        /// This may differ from what was requested with <see cref="MRUKSettings.TrackerConfiguration"/> if, for example, some types of trackables are not supported on the current device.
        /// </remarks>
        public OVRAnchor.TrackerConfiguration TrackerConfiguration { get; private set; }

        /// <summary>
        /// Get all the trackables that have been detected so far.
        /// </summary>
        /// <param name="trackables">The list to populate with the trackables. The list is cleared before adding any elements.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="trackables"/> is `null`.</exception>
        public void GetTrackables(List<MRUKTrackable> trackables)
        {
            if (trackables == null)
            {
                throw new ArgumentNullException(nameof(trackables));
            }

            trackables.Clear();
            foreach (var trackable in _trackables.Values)
            {
                if (trackable)
                {
                    trackables.Add(trackable);
                }
            }
        }

        private readonly Dictionary<ulong, MRUKTrackable> _trackables = new();

        private bool _hasScenePermission;

        private OVRAnchor.TrackerConfiguration _lastRequestedConfiguration;

        private TimeSpan _nextTrackerConfigurationTime;

        // 0.5 seconds because most of our trackers update at about 1 Hz
        private static readonly TimeSpan s_timeBetweenTrackerConfigurationAttempts = TimeSpan.FromSeconds(0.5);

        private void UpdateTrackables()
        {
            var now = TimeSpan.FromSeconds(Time.realtimeSinceStartup);

            // We should only try to set the tracker configuration if
            // 1. The requested configuration has changed since last time
            // 2. The actual tracker configuration does not match the requested one, but permissions have also changed, which may now allow one of the failing requests to succeed.
            var desiredConfig = SceneSettings.TrackerConfiguration;
            if (_configureTrackersTask.HasValue ||
                TrackerConfiguration == desiredConfig ||
                now < _nextTrackerConfigurationTime)
            {
                return;
            }

            var hasScenePermissionBeenGrantedSinceLastCheck = false;
            if (!_hasScenePermission && Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
            {
                _hasScenePermission = hasScenePermissionBeenGrantedSinceLastCheck = true;
            }

            // Keeping track of the _lastRequestedConfiguration allows us to avoid repeatedly asking for the same
            // configuration if that configuration fails.
            if (_lastRequestedConfiguration != desiredConfig ||
                hasScenePermissionBeenGrantedSinceLastCheck)
            {
                _lastRequestedConfiguration = desiredConfig;
                ConfigureTrackerAndLogResult(desiredConfig);
            }

            _nextTrackerConfigurationTime = now + s_timeBetweenTrackerConfigurationAttempts;
        }

        private void OnDisable()
        {
            if (MRUKNativeFuncs.ConfigureTrackers != null)
            {
                MRUKNativeFuncs.ConfigureTrackers(0);
            }
            _configureTrackersTask = null;
            _lastRequestedConfiguration = TrackerConfiguration = default;
            _nextTrackerConfigurationTime = TimeSpan.Zero;
        }

        private async void ConfigureTrackerAndLogResult(OVRAnchor.TrackerConfiguration config)
        {
            Debug.Assert(_configureTrackersTask == null);

            uint trackableMask = 0;
            if (config.KeyboardTrackingEnabled)
            {
                trackableMask |= (uint)MRUKNativeFuncs.MrukTrackableType.Keyboard;
            }

            if (config.QRCodeTrackingEnabled)
            {
                trackableMask |= (uint)MRUKNativeFuncs.MrukTrackableType.Qrcode;
            }

            _configureTrackersTask = OVRTask.Create<MRUKNativeFuncs.MrukResult>(Guid.NewGuid());
            MRUKNativeFuncs.ConfigureTrackers(trackableMask);

            var result = await _configureTrackersTask.Value;
            if (result == MRUKNativeFuncs.MrukResult.Success)
            {
                Debug.Log($"Configured anchor trackers: {config}");
            }
            else
            {
                Debug.LogWarning($"{result}: Unable to fully satisfy requested tracker configuration. Requested={config}.");
            }

            if (this)
            {
                _configureTrackersTask = null;
                if (result == MRUKNativeFuncs.MrukResult.Success)
                {
                    TrackerConfiguration = config;
                }
            }
        }

        private void HandleTrackableAdded(ref MRUKNativeFuncs.MrukTrackable trackable)
        {
            if (_trackables.ContainsKey(trackable.space))
            {
                Debug.LogWarning($"{nameof(HandleTrackableAdded)}: Trackable {trackable.uuid} of type {trackable.trackableType} was previously added. Ignoring.");
                return;
            }

            var go = new GameObject($"Trackable({trackable.trackableType}) {trackable.uuid}");
            go.transform.SetParent(OVRCameraRig.GetTrackingSpace(), worldPositionStays: false);
            var component = go.AddComponent<MRUKTrackable>();
            _trackables.Add(trackable.space, component);

            UpdateTrackableProperties(component, ref trackable);

            // Notify user
            SceneSettings.TrackableAdded.Invoke(component);
        }

        private void HandleTrackableUpdated(ref MRUKNativeFuncs.MrukTrackable trackable)
        {
            if (_trackables.TryGetValue(trackable.space, out var component) && component)
            {
                UpdateTrackableProperties(component, ref trackable);
            }
        }

        private void HandleTrackableRemoved(ref MRUKNativeFuncs.MrukTrackable trackable)
        {
            if (_trackables.Remove(trackable.space, out var component) && component)
            {
                component.IsTracked = false;
                SceneSettings.TrackableRemoved.Invoke(component);
            }
        }
    }
}
