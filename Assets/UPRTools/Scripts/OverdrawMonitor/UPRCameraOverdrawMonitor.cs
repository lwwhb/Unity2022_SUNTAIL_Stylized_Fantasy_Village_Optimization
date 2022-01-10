#if UNITY_2018_2_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UPRProfiler.Common;

namespace UPRProfiler.OverdrawMonitor
{
    public class UPRCameraOverdrawMonitor : MonoBehaviour
    {
        public static bool EnableOverdrawScreenshot = true;
        
        Camera _targetCamera;
        string _cameraName;

        public Camera targetCamera
        {
            get => _targetCamera;
            set => _targetCamera = value;
        }

        public void SetTargetCamera(Camera targetCamera)
        {
            _targetCamera = targetCamera;
            _cameraName = targetCamera.name;
        }

        Shader _replacementShader;
        RenderTexture _overdrawTexture;
        Texture2D _readingTexture;
        Rect _readingRect;
        Dictionary<string, string> _toSendData = new Dictionary<string, string>(1);

        public static int MonitorFrequency = 30;
        int _index;
        int _calBreakSize;

        bool _allBlack;
        int _allBlackFrameCount;

        static float oneDrawTimeG = 0.04f;
        public static string customDataSubjectName = "UPROverdraw";
        public static string customDataGroupName = "OverdrawRate";

        void Awake()
        {
            _replacementShader = Shader.Find("Debug/OverdrawColor");
            _index = MonitorFrequency - 1;
        }

        void OnDestroy()
        {
            _toSendData[_cameraName] = "0";
            UPROpen.SendCustomizedData(customDataSubjectName, customDataGroupName, "line", _toSendData);
            ReleaseTexture();
        }

        void InitTexture()
        {
            int overDrawRTWidth, overDrawRTHeight;
            if (_targetCamera.pixelHeight > 480)
            {
                overDrawRTWidth =
                    Mathf.FloorToInt(480.0f * _targetCamera.pixelWidth / _targetCamera.pixelHeight);
                overDrawRTHeight = 480;
            }
            else
            {
                overDrawRTWidth = _targetCamera.pixelWidth;
                overDrawRTHeight = _targetCamera.pixelHeight;
            }

            _overdrawTexture = new RenderTexture(overDrawRTWidth, overDrawRTHeight, 0, RenderTextureFormat.ARGBFloat);
            _readingTexture = new Texture2D(overDrawRTWidth, overDrawRTHeight, TextureFormat.RGBAFloat, false);
            _readingRect = new Rect(0, 0, _overdrawTexture.width, _overdrawTexture.height);
            _calBreakSize = overDrawRTWidth * overDrawRTHeight / (MonitorFrequency - 2);
            _toSendData[_cameraName] = "0";
            UPROpen.SendCustomizedData(customDataSubjectName, customDataGroupName, "line", _toSendData);
        }

        void ReleaseTexture()
        {
            if (_overdrawTexture != null)
            {
                _overdrawTexture.Release();
                _overdrawTexture = null;
                Destroy(_readingTexture);
                _readingTexture = null;
                _readingRect = Rect.zero;
            }
        }

        private void LateUpdate()
        {
            
            _index++;
            if (_index == MonitorFrequency)
            {
                _index = 0;
            }
            else
            {
                return;
            }

            if (_targetCamera == null)
            {
                return;
            }
            Profiler.BeginSample("Profiler.UPRCameraOverdrawMonitor");
            CameraClearFlags originalClearFlags = _targetCamera.clearFlags;
            Color originalClearColor = _targetCamera.backgroundColor;
            RenderTexture originalTargetTexture = _targetCamera.targetTexture;
            bool originalIsCameraEnabled = _targetCamera.enabled;

            if (_overdrawTexture == null)
            {
                ReleaseTexture();
                InitTexture();
            }

            _targetCamera.clearFlags = CameraClearFlags.SolidColor;
            _targetCamera.backgroundColor = Color.black;
            _targetCamera.targetTexture = _overdrawTexture;
            _targetCamera.enabled = false;

            _targetCamera.RenderWithShader(_replacementShader, null);

            RenderTexture.active = _overdrawTexture;
            _readingTexture.ReadPixels(_readingRect, 0, 0);
            _readingTexture.Apply();
            RenderTexture.active = null;

            StartCoroutine(CalculateOverdraw());

            _targetCamera.targetTexture = originalTargetTexture;
            _targetCamera.clearFlags = originalClearFlags;
            _targetCamera.backgroundColor = originalClearColor;
            _targetCamera.enabled = originalIsCameraEnabled;
            Profiler.EndSample();
        }

        IEnumerator CalculateOverdraw()
        {
            _allBlack = true;
            yield return null;
            Profiler.BeginSample("Profiler.UPROverdrawMonitorCal");
            var overdrawColors = _readingTexture.GetRawTextureData<Color>();
            int totalSize = overdrawColors.Length;
            var breakPoint = _calBreakSize;
            float drawTimesInG = 0f;
            for (var i = 0; i < totalSize; i++)
            {
                if (overdrawColors[i].g <= oneDrawTimeG)
                {
                    drawTimesInG += oneDrawTimeG;
                }
                else
                {
                    if (_allBlack)
                    {
                        _allBlack = false;
                    }

                    drawTimesInG += overdrawColors[i].g;
                }

                if (i == breakPoint)
                {
                    breakPoint += _calBreakSize;
                    Profiler.EndSample();
                    yield return null;
                    Profiler.BeginSample("Profiler.UPROverdrawMonitorCal");
                }
            }

            if (_allBlack)
            {
                _allBlackFrameCount++;
                if (_allBlackFrameCount > 3)
                {
                    UPROverdrawMonitor.NotSupportedPlatform = true;
                    UPROverdrawMonitor.NotSupportedMessageGroupName = "allBlack";
                }
            }
            else
            {
                var overdrawRate = Convert.ToInt32(drawTimesInG / oneDrawTimeG) / (float) totalSize;
                _toSendData[_cameraName] = overdrawRate < 1f ? "1" : overdrawRate.ToString();
                UPROpen.SendCustomizedData(customDataSubjectName, customDataGroupName, "line", _toSendData);
                if (EnableOverdrawScreenshot)
                {
                    NetworkServer.SendOverdrawScreenshot(_readingTexture.EncodeToJPG(), _cameraName);
                }
            }
            
            Profiler.EndSample();
        }
    }
}
#endif