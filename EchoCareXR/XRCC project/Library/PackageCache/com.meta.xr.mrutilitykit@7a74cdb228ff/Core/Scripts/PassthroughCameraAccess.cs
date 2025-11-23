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
using System.Collections;
using Meta.XR.MRUtilityKit;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace Meta.XR
{
    /// <summary>
    /// Provides access to the Passthrough Camera Access API.<br/>
    /// This component requires the "horizonos.permission.HEADSET_CAMERA" permission to be present in AndroidManifest.xml:
    /// <code>&lt;uses-permission android:name="horizonos.permission.HEADSET_CAMERA" /&gt;</code>
    /// See documentation: https://developers.meta.com/horizon/documentation/unity/unity-pca-overview/
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PassthroughCameraAccess : MonoBehaviour
    {
        private const string _cameraPermission = OVRPermissionsRequester.PassthroughCameraAccessPermission; // Required to access the Passthrough Camera API in Horizon OS v74 and above.
        private const string _noPermissionMessage = nameof(PassthroughCameraAccess) + " doesn't have the required camera permission: " + _cameraPermission +
                                                   ". Waiting for permission before enabling the camera...\n" +
                                                   "To request camera permission, AndroidManifest.xml should contain this entry:\n" +
                                                   "<uses-permission android:name=\"horizonos.permission.HEADSET_CAMERA\" />";

        /// <summary>
        /// Possible positions of the camera (Left or Right).
        /// </summary>
        public enum CameraPositionType { Left, Right }
        /// <summary>Requested camera position. To access both left and right camera simultaneously, create two instances of <see cref="PassthroughCameraAccess"/> with different <see cref="PassthroughCameraAccess.CameraPosition"/></summary>
        [SerializeField] public CameraPositionType CameraPosition;
        /// <summary>The requested resolution of the camera. If the requested resolution is not present in <see cref="GetSupportedResolutions"/>, the first smaller resolution will be selected instead.</summary>
        [SerializeField] public Vector2Int RequestedResolution = new Vector2Int(1280, 960);

        [Tooltip("If set, PassthroughCameraAccess will assign its internal Texture2D into this Material each frame.")]
        [SerializeField] public Material TargetMaterial;

        [Tooltip("(Optional) The name of the texture property to update. If blank, uses _MainTex.")]
        [SerializeField] private string _texturePropertyName = "";

        private int _texturePropertyID;
        private static readonly PassthroughCameraAccess[] _instances = new PassthroughCameraAccess[2];
        private Texture2D _texture;
        private bool _updateTextureAutomatically;
        private int _currentCameraIndex;

        private bool? _isPlaying;
        private int _lastUpdatedImageFrame;
        private bool _wasPlayingBeforePause;
        private long _timestampNsMonotonic;

        /// <summary>
        /// The name of the texture property to update. If changed at runtime,
        /// we recache the shader property ID so that Material.SetTexture(...) uses the new name.
        /// </summary>
        public string TexturePropertyName
        {
            get => _texturePropertyName;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "TexturePropertyName cannot be null.");
                }

                if (_texturePropertyName == value)
                {
                    return;
                }

                _texturePropertyName = value;
                _texturePropertyID = Shader.PropertyToID(string.IsNullOrEmpty(value) ? "_MainTex" : value);
            }
        }

        /// <summary>Indicates whether the component is enabled and has received the camera image at least once.</summary>
        public bool IsPlaying => _isPlaying == true;

        /// <summary>Timestamp associated with the current camera image.</summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>The current resolution of camera. May be different from the <see cref="RequestedResolution"/>.</summary>
        public Vector2Int CurrentResolution { get; private set; }

        /// <summary>The static intrinsic parameters of the sensor. These parameters become available after the <see cref="PassthroughCameraAccess"/> is enabled and never change after that.</summary>
        public CameraIntrinsics Intrinsics { get; private set; }

        /// <summary>
        /// Retrieves color data of the latest camera image. Use this method to process camera images on CPU.<br/>
        /// Do not cache, modify or dispose the contents of the returned native array.
        /// </summary>
        /// <returns>Native array that contains all pixels of the captured image.</returns>
        public NativeArray<Color32> GetColors()
        {
            if (!ValidateIsEnabled())
            {
                return default;
            }
            return _texture.GetRawTextureData<Color32>();
        }

        /// <summary>Retrieves GPU texture of the latest camera image. Use this method to access camera images on GPU.</summary>
        /// <returns>Texture with the latest camera image.</returns>
        public Texture GetTexture()
        {
            if (!ValidateIsEnabled())
            {
                return null;
            }
            if (!_updateTextureAutomatically)
            {
                _updateTextureAutomatically = true;
                _texture.Apply();
            }
            return _texture;
        }

        private bool ValidateIsEnabled()
        {
            if (enabled)
            {
                return true;
            }
            Debug.LogError(nameof(PassthroughCameraAccess) + " is not enabled. Please enable the component before accessing this API.", this);
            return false;
        }

        /// <summary>Returns 'true' if the camera texture was updated this frame.</summary>
        public bool IsUpdatedThisFrame => _lastUpdatedImageFrame == Time.frameCount;

        /// <summary>Returns true if the current headset supports Passthrough Camera Access.</summary>
        public static bool IsSupported
        {
            get
            {
                using var vrosClass = new AndroidJavaClass("vros.os.VrosBuild");
                var osVersion = vrosClass.CallStatic<int>("getSdkVersion");
                if (osVersion == 10000)
                {
                    return false;
                }
                const int minSupportedVersion = 74;
                var headset = OVRPlugin.GetSystemHeadsetType();
                return (headset is OVRPlugin.SystemHeadset.Meta_Quest_3 or OVRPlugin.SystemHeadset.Meta_Quest_3S) && osVersion >= minSupportedVersion;
            }
        }

        private unsafe void Update()
        {
            if (!_isPlaying.HasValue || !_texture)
            {
                return;
            }
            long timestampMicroseconds = 0;
            int eyeIndex = GetCameraIndex(CameraPosition);
            byte* buffer = MRUKNativeFuncs.CameraAcquireLatestImage(eyeIndex, ref timestampMicroseconds, ref _timestampNsMonotonic);
            try
            {
                if (buffer != null)
                {
                    _texture.LoadRawTextureData((IntPtr)buffer, CurrentResolution.x * CurrentResolution.y * sizeof(Color32));
                }
            }
            finally
            {
                MRUKNativeFuncs.CameraReleaseLatestImage(eyeIndex);
            }

            if (buffer == null)
            {
                return;
            }

            _isPlaying = true;
            const long ticksPerMicrosecond = 10;
            Timestamp = DateTime.UnixEpoch.AddTicks(timestampMicroseconds * ticksPerMicrosecond);
            _lastUpdatedImageFrame = Time.frameCount;

            if (_updateTextureAutomatically || TargetMaterial)
            {
                _texture.Apply();
            }

            UpdateMaterialTexture();
        }

        private void UpdateMaterialTexture()
        {
            if (TargetMaterial)
            {
                if (TargetMaterial.HasProperty(_texturePropertyID))
                {
                    TargetMaterial.SetTexture(_texturePropertyID, _texture);
                }
                else
                {
                    Debug.LogWarning($"Texture property '{_texturePropertyName}' not found on the material for GameObject {gameObject.name}.", this);
                }
            }
        }

        private void Awake()
        {
            if (_texturePropertyName == null)
            {
                throw new InvalidOperationException("_texturePropertyName cannot be null.");
            }

            _texturePropertyID = Shader.PropertyToID(string.IsNullOrEmpty(_texturePropertyName) ? "_MainTex" : _texturePropertyName);
        }

        private void Start() => OVRTelemetry.Start(TelemetryConstants.MarkerId.LoadPassthroughCameraAccess).Send();

        private void OnEnable()
        {
            if (Permission.HasUserAuthorizedPermission(_cameraPermission))
            {
                InitAndPlay();
            }
            else
            {
                Debug.LogWarning(_noPermissionMessage);
                StartCoroutine(WaitForPermissionsAndPlay());
            }
        }

        private IEnumerator WaitForPermissionsAndPlay()
        {
            while (!Permission.HasUserAuthorizedPermission(_cameraPermission))
            {
                yield return null;
            }
            InitAndPlay();
        }

        private void OnDisable()
        {
            StopCoroutine(WaitForPermissionsAndPlay());
            if (_isPlaying.HasValue)
            {
                Stop();
                if (_texture != null)
                {
                    Destroy(_texture);
                    _texture = null;
                }
                CurrentResolution = Vector2Int.zero;
                _updateTextureAutomatically = false;
            }
        }

        private void OnApplicationFocus(bool isFocused)
        {
            if (isFocused)
            {
                MRUKNativeFuncs.CameraOnApplicationFocused?.Invoke();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            Log($"OnApplicationPause {isPaused} {_currentCameraIndex}, CurrentResolution:{CurrentResolution}");
            if (isPaused)
            {
                if (_isPlaying.HasValue)
                {
                    _wasPlayingBeforePause = true;
                    Stop();
                }
            }
            else if (_wasPlayingBeforePause)
            {
                _wasPlayingBeforePause = false;
                Play(CurrentResolution);
            }
        }

        private void InitAndPlay()
        {
            Assert.IsFalse(_isPlaying.HasValue);
            bool TryPlay()
            {
                _currentCameraIndex = GetCameraIndex(CameraPosition);
                if (_instances[_currentCameraIndex] != null)
                {
                    Debug.LogError($"Only one instance of {nameof(PassthroughCameraAccess)} is allowed per camera position ({_currentCameraIndex}). Please ensure you're not creating two instances with the same '{nameof(CameraPosition)}'.", this);
                    return false;
                }
                Play(RequestedResolution);
                if (_isPlaying.HasValue)
                {
                    _texture = new Texture2D(CurrentResolution.x, CurrentResolution.y, TextureFormat.RGBA32, false);
                    return true;
                }
                return false;
            }
            if (!TryPlay())
            {
                enabled = false;
            }
        }

        private void Play(Vector2Int resolution)
        {
            int w = resolution.x;
            int h = resolution.y;
            MRUKNativeFuncs.MrukCameraIntrinsics intrinsics = default;
            if (MRUKNativeFuncs.CameraPlay(_currentCameraIndex, ref w, ref h, ref intrinsics))
            {
                Log($"Play() {_currentCameraIndex} {w}x{h}");
                _instances[_currentCameraIndex] = this;

                if (resolution != Vector2Int.zero && resolution != new Vector2Int(w, h))
                {
                    Debug.LogWarning($"Requested resolution {resolution} is not supported, using ({w}, {h}) instead.");
                }
                Assert.IsFalse(_isPlaying.HasValue);
                _isPlaying = false;
                CurrentResolution = new Vector2Int(w, h);
                var lensTranslation = intrinsics.lensTranslation;
                var lensRotation = intrinsics.lensRotation;
                Intrinsics = new CameraIntrinsics
                {
                    FocalLength = intrinsics.focalLength,
                    PrincipalPoint = intrinsics.principalPoint,
                    SensorResolution = intrinsics.sensorResolution,
                    LensOffset = new Pose(MRUK.FlipZ(lensTranslation), Quaternion.Inverse(new Quaternion(-lensRotation[0], -lensRotation[1], lensRotation[2], lensRotation[3])) * Quaternion.Euler(180, 0, 0))
                };
            }
            else if (w == -1 || h == -1)
            {
                Debug.LogError($"Requested resolution {resolution} is too small.", this);
            }
            else
            {
                Debug.LogError($"{nameof(PassthroughCameraAccess)} failed to play camera at index {_currentCameraIndex}.", this);
            }
        }

        private void Stop()
        {
            Log($"Stop() {_currentCameraIndex}");
            _instances[_currentCameraIndex] = null;
            MRUKNativeFuncs.CameraStop(_currentCameraIndex);
            _isPlaying = null;
            Timestamp = default;
            Intrinsics = default;
        }

        private static int GetCameraIndex(CameraPositionType cameraPosition)
        {
            switch (cameraPosition)
            {
                case CameraPositionType.Left:
                    return 0;
                case CameraPositionType.Right:
                    return 1;
                default: throw new Exception($"Invalid camera position: {cameraPosition}.");
            }
        }

        /// <summary>Retrieves supported resolutions of the given camera.</summary>
        /// <param name="cameraPosition">The position of the camera (Left or Right).</param>
        /// <returns>An array containing all supported resolutions.</returns>
        public static unsafe Vector2Int[] GetSupportedResolutions(CameraPositionType cameraPosition)
        {
            if (!Permission.HasUserAuthorizedPermission(_cameraPermission))
            {
                Debug.LogError(_noPermissionMessage);
                return Array.Empty<Vector2Int>();
            }
            int cameraIndex = GetCameraIndex(cameraPosition);
            int len = 0;
            Vector2Int* buffer = MRUKNativeFuncs.CameraGetSupportedResolutions(cameraIndex, ref len);
            var result = new Vector2Int[len];
            for (uint i = 0; i < len; ++i)
            {
                result[i] = buffer[i];
            }
            MRUKNativeFuncs.CameraFreeSupportedResolutions(buffer);
            return result;
        }

        /// <summary>Returns a world-space ray going from camera through a viewport point.</summary>
        /// <param name="viewportPoint">Viewport-space is normalized and relative to the camera. The bottom-left of the camera is (0,0); the top-right is (1,1).</param>
        /// <returns>World-space ray.</returns>
        public Ray ViewportPointToRay(Vector2 viewportPoint)
        {
            if (!ValidateIsPlaying())
            {
                return default;
            }
            var cameraPose = GetCameraPose();
            var direction = cameraPose.rotation * ViewportPointToLocalRay(viewportPoint).direction;
            return new Ray(cameraPose.position, direction);
        }

        private Ray ViewportPointToLocalRay(Vector2 viewportPoint)
        {
            var intrinsics = Intrinsics;
            var sensorCropRegion = CalcSensorCropRegion();
            var principalPoint = intrinsics.PrincipalPoint;
            var focalLength = intrinsics.FocalLength;
            var directionInCamera = new Vector3
            {
                x = (sensorCropRegion.x + sensorCropRegion.width * viewportPoint.x - principalPoint.x) / focalLength.x,
                y = (sensorCropRegion.y + sensorCropRegion.height * viewportPoint.y - principalPoint.y) / focalLength.y,
                z = 1
            };

            return new Ray(Vector3.zero, directionInCamera);
        }

        /// <summary>Transforms <paramref name="worldPosition"/> from world-space into viewport-space.</summary>
        /// <param name="worldPosition">A world-space position.</param>
        /// <returns>Viewport-space coordinate. Viewport-space is normalized and relative to the camera. The bottom-left of the camera is (0,0); the top-right is (1,1).</returns>
        public Vector2 WorldToViewportPoint(Vector3 worldPosition)
        {
            if (!ValidateIsPlaying())
            {
                return default;
            }
            Pose cameraPose = GetCameraPose();
            Vector3 positionInCameraSpace = Quaternion.Inverse(cameraPose.rotation) * (worldPosition - cameraPose.position);
            var intrinsics = Intrinsics;
            var focalLength = intrinsics.FocalLength;
            var principalPoint = intrinsics.PrincipalPoint;
            var sensorPoint = new Vector2(
                (positionInCameraSpace.x / positionInCameraSpace.z) * focalLength.x + principalPoint.x,
                (positionInCameraSpace.y / positionInCameraSpace.z) * focalLength.y + principalPoint.y);
            var sensorCropRegion = CalcSensorCropRegion();
            return new Vector2(
                (sensorPoint.x - sensorCropRegion.x) / sensorCropRegion.width,
                (sensorPoint.y - sensorCropRegion.y) / sensorCropRegion.height);
        }

        private Rect CalcSensorCropRegion()
        {
            var sensorResolution = (Vector2)Intrinsics.SensorResolution;
            var currentResolution = (Vector2)CurrentResolution;
            Vector2 scaleFactor = currentResolution / sensorResolution;
            scaleFactor /= Mathf.Max(scaleFactor.x, scaleFactor.y);
            return new Rect(
                sensorResolution.x * (1f - scaleFactor.x) * 0.5f,
                sensorResolution.y * (1f - scaleFactor.y) * 0.5f,
                sensorResolution.x * scaleFactor.x,
                sensorResolution.y * scaleFactor.y);
        }

        /// <returns>Camera's world-space pose at <see cref="Timestamp"/>.</returns>
        public Pose GetCameraPose()
        {
            if (!ValidateIsPlaying())
            {
                return default;
            }
            var headPose = OVRPlugin.GetNodePoseStateAtTime(GetMonotonicTimestamp(), OVRPlugin.Node.Head).Pose.ToOVRPose();
            var lensOffset = Intrinsics.LensOffset;
            return new Pose(headPose.position + headPose.orientation * lensOffset.position,
                headPose.orientation * lensOffset.rotation);
        }

        private double GetMonotonicTimestamp()
        {
#if UNITY_EDITOR
            if (!OVRPlugin.initialized)
            {
                return OVRPlugin.GetTimeInSeconds();
            }
#endif
            return MRUKNativeFuncs.ConvertToXrTimeInSeconds(_timestampNsMonotonic);
        }

        private bool ValidateIsPlaying()
        {
            if (IsPlaying)
            {
                return true;
            }
            Debug.LogError(nameof(PassthroughCameraAccess) + " is not playing. Please check the '" + nameof(IsPlaying) + "' before calling this API.", this);
            return false;
        }

        /// <summary>The static intrinsic parameters of the sensor.</summary>
        public struct CameraIntrinsics
        {
            /// <summary>The focal length of the camera.</summary>
            public Vector2 FocalLength;
            /// <summary>The principal point of the camera.</summary>
            public Vector2 PrincipalPoint;
            /// <summary>The sensor resolution of the camera.</summary>
            public Vector2Int SensorResolution;
            /// <summary>The translation and orientation of the camera sensor relative to the headset.</summary>
            public Pose LensOffset;
        }

        [System.Diagnostics.Conditional("DEBUG_PASSTHROUGH_CAMERA_ACCESS")]
        private void Log(object msg) => Debug.LogWarning($"PassthroughCamera {_currentCameraIndex}: {msg}");
    }
}
