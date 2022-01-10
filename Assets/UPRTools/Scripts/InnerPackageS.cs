using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.Text;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using System.Reflection;
using UPRProfiler.Common;
using UPRProfiler.Mail;
#if UNITY_2018_2_OR_NEWER
using Unity.Collections;
#endif

namespace UPRProfiler
{
    public class InnerPackageS : MonoBehaviour
    {
        private static int pid = 0;
        private static AndroidJavaClass UnityPlayer;
        private static AndroidJavaObject currentActivity;
        private static AndroidJavaObject memoryManager;
        private static AndroidJavaObject[] memoryInfoArray;
        private static AndroidJavaObject memoryInfo;
        private static AndroidJavaObject intentFilter;
        private static AndroidJavaObject intent;
        private static object[] tempeartureModel = new object[] { "temperature", 0 };
        private static int width;
        private static int height;
        private static byte[] rawBytes;
        public static WaitForSeconds waitOneSeconds = new WaitForSeconds(4);
        

        private static int gpuCounters = 0;
        public static GUIStyle fontStyle;
        private static Dictionary<string, string> memoryDict;
        private static Dictionary<string, string> wwiseDict;
//        private static string[] pssArray = new string[6];
#if UNITY_2018_2_OR_NEWER
        private static NativeArray<byte> nativeRawBytes;
#endif


        void Start()
        {
            height = Screen.height;
            width = Screen.width;
            fontStyle = new GUIStyle();
            fontStyle.normal.background = null;    //设置背景填充
            fontStyle.normal.textColor= new Color(1,0,0);   //设置字体颜色
            fontStyle.fontSize = 20;

#if !UNITY_2018_2_OR_NEWER
        rawBytes = new byte[0];
#endif
            StartCoroutine(GetScreenShot());

            var wWiseSummaryClass = Type.GetType("AkResourceMonitorDataSummary");
            object wWiseSummaryInstance = wWiseSummaryClass == null ? null : Activator.CreateInstance(wWiseSummaryClass);
            if(wWiseSummaryInstance != null)
            {
                var soundEngineClass = Type.GetType("AkSoundEngine");
                object soundEngineInstance = soundEngineClass == null ? null : Activator.CreateInstance(soundEngineClass);
                MethodInfo monitorMethod = soundEngineClass.GetMethod("StartResourceMonitoring");

                if(monitorMethod != null) {
                    monitorMethod.Invoke(soundEngineInstance, null);
                    StartCoroutine(GetWWise(wWiseSummaryInstance, soundEngineClass, soundEngineInstance));
                }
            }

            if (Application.platform == RuntimePlatform.Android)
            {
                StartCoroutine(GetGPUCounter());
                UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                currentActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaClass process = new AndroidJavaClass("android.os.Process");
                intentFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED");
                intent = currentActivity.Call<AndroidJavaObject>("registerReceiver", new object[] { null, intentFilter });
                pid = process.CallStatic<int>("myPid");
                if (pid > 0)
                {
                    memoryManager = currentActivity.Call<AndroidJavaObject>("getSystemService", new AndroidJavaObject("java.lang.String", "activity"));
                    memoryInfoArray = memoryManager.Call<AndroidJavaObject[]>("getProcessMemoryInfo", new int[] { pid });
                    memoryInfo = memoryInfoArray[0];
                    StartCoroutine(GetSystemMemory());
                }
                else
                {
                    Debug.LogError("Get Device Pid Error");
                }
            }

        }

        IEnumerator GetScreenShot()
        {
            WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
            Texture2D shot = null;
            Rect area = Rect.zero;
            width = 0;
            while (true)
            {
                yield return waitForEndOfFrame;
                if (NetworkServer.isConnected && NetworkServer.enableScreenShot && !NetworkServer.screenFlag)
                {
                    if (!shot)
                    {
                        shot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                        shot.name = "upr_screenshot";
                    }
                    
                    if (width != Screen.width)
                    {
                        shot.Reinitialize(Screen.width, Screen.height);
                        width = Screen.width;
                        height = Screen.height;
                        area = new Rect(0, 0, Screen.width, Screen.height);
                        yield return waitForEndOfFrame;
                    }
                    Profiler.BeginSample("Profiler.ScreenShotCoroutine");
                    try
                    {
                        shot.ReadPixels(area, 0, 0, false);
                        shot.Apply();
#if UNITY_2018_2_OR_NEWER
                        nativeRawBytes = shot.GetRawTextureData<byte>();
                        NetworkServer.screenFlag = true;
                        NetworkServer.SendMessage(nativeRawBytes, 0, width, height);
#else
                    rawBytes = shot.GetRawTextureData();
                    NetworkServer.screenFlag = true;
                    screenCnt++;
                    NetworkServer.SendMessage(rawBytes, 0, width, height);
#endif
                    }
                    catch (Exception e)
                    {
                        NetworkServer.screenFlag = false;
                        Debug.LogError("[PACKAGE] Screenshot Error " + e);
                    }
                    Profiler.EndSample();
                }
               
                if (NetworkServer.isConnected && !NetworkServer.sendDeviceInfo)
                {
                    try
                    {
                        GetSystemDevice();
                        NetworkServer.sendDeviceInfo = true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[PACKAGE] Send SystemDevice Error " + e);
                    }
                }
                
                yield return waitOneSeconds;
            }
        }

        IEnumerator GetSystemMemory()
        {
            WaitForSeconds waitOneSeconds = new WaitForSeconds(3);
            memoryDict = new Dictionary<string, string>();
            while (true)
            {

                Profiler.BeginSample("Profiler.GetMemoryInfo");
                if (NetworkServer.isConnected && NetworkServer.enableScreenShot)
                {
                    try
                    {
                        GetPSS();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[PACKAGE] Send PSS Error " + ex);
                    }
                    
                }
                Profiler.EndSample();  
                yield return waitOneSeconds;
            }
        }

        IEnumerator GetWWise(object wWiseSummaryInstance, Type soundEngineClass, object soundEngineInstance)
        {
            MethodInfo getSummary = soundEngineClass.GetMethod("GetResourceMonitorDataSummary");
            WaitForSeconds waitOneSeconds = new WaitForSeconds(1);
            wwiseDict = new Dictionary<string, string>();
            while (true)
            {
                Profiler.BeginSample("Profiler.GetWWiseInfo");
                if (NetworkServer.isConnected)
                {
                    try
                    {
                        getSummary.Invoke(soundEngineInstance, new object[] { wWiseSummaryInstance });
                        wwiseDict.Clear();
                        wwiseDict["CPU"] = GetPropValue(wWiseSummaryInstance, "totalCPU").ToString();
                        wwiseDict["pluginCPU"] = GetPropValue(wWiseSummaryInstance, "pluginCPU").ToString();
                        UPROpen.SendCustomizedData("wWise", "wWiseCPU", "line", wwiseDict);
                        
                        wwiseDict.Clear();
                        wwiseDict["voice"] = GetPropValue(wWiseSummaryInstance, "totalVoices").ToString();
                        wwiseDict["virtualVoice"] = GetPropValue(wWiseSummaryInstance, "virtualVoices").ToString();
                        wwiseDict["physicalVoice"] = GetPropValue(wWiseSummaryInstance, "physicalVoices").ToString();
                        UPROpen.SendCustomizedData("wWise", "wWiseVoice", "line", wwiseDict);
                        
                        wwiseDict.Clear();
                        wwiseDict["events"] = GetPropValue(wWiseSummaryInstance, "nbActiveEvents").ToString();
                        UPROpen.SendCustomizedData("wWise", "wWiseEvents", "line", wwiseDict);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[PACKAGE] Send WWise Error " + ex);
                    }
                }
                Profiler.EndSample();
                yield return waitOneSeconds;
            }

        }

        IEnumerator GetGPUCounter()
        {
            WaitForSeconds waitOneSeconds = new WaitForSeconds(1);
            HWCPipe.Start();
            
            gpuCounters = HWCPipe.GPU_GetNumCounters();
            for (int i = 0; i < gpuCounters; i++)
            {
                HWCPipe.GPU_EnableCounter(i);
            }
            
            StringBuilder gpuInfo = new StringBuilder();
            while (true)
            {
                Profiler.BeginSample("Profiler.GPUMail");
                if (NetworkServer.isConnected && NetworkServer.enableGPUProfiler)
                {
                    try
                    {
                        HWCPipe.Sample();
                        gpuInfo.Remove(0, gpuInfo.Length);
                        for (int i = 0; i < gpuCounters; i++)
                        {
                            if (HWCPipe.GPU_IsCounterSupported(i))
                                gpuInfo.AppendFormat("{0}:{1}&", Enum.GetName(typeof(HWCPipe.GpuCounter), i),
                                HWCPipe.GPU_GetCounterValue(i));
                        }
                        if (gpuInfo.Length > 0)
                            NetworkServer.SendMessage(Encoding.ASCII.GetBytes(gpuInfo.ToString()), 5, width, height);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[PACKAGE] Send GPU Error " + e);
                    }
                    
                }
                Profiler.EndSample();
                yield return waitOneSeconds;
            }
//            HWCPipe.Stop();
        }
        
        public static object GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName).GetValue(src, null);
        }


        public static void GetPSS()
        {
            memoryDict.Clear();
            memoryDict["battery"] = SystemInfo.batteryLevel.ToString();
            UPROpen.SendCustomizedData("batteryHome", "batteryChart", "line", memoryDict);
            
            memoryDict.Clear();
            memoryDict["cpuTemp"] = intent.Call<int>("getIntExtra", tempeartureModel).ToString();
            UPROpen.SendCustomizedData("batteryHome", "tempChart", "line", memoryDict);
        }

        public static void GetSystemDevice()
        {
            StringBuilder deviceInfo = new StringBuilder();
            deviceInfo.AppendFormat("{0}:{1}", "systemBrand", SystemInfo.deviceModel);
            deviceInfo.AppendFormat("& {0}:{1}", "systemTotalRam", SystemInfo.systemMemorySize.ToString() + "MB");
            deviceInfo.AppendFormat("& {0}:{1}", "systemMaxCpuFreq", SystemInfo.processorFrequency + "MHZ");
            deviceInfo.AppendFormat("& {0}:{1}", "packageVersion", Utils.PackageVersion);
            
            deviceInfo.AppendFormat("& {0}:{1}", "operatingSystem", SystemInfo.operatingSystem);
            deviceInfo.AppendFormat("& {0}:{1}", "graphicsDeviceID", SystemInfo.graphicsDeviceID);
            deviceInfo.AppendFormat("& {0}:{1}", "graphicsDeviceName", SystemInfo.graphicsDeviceName);
            deviceInfo.AppendFormat("& {0}:{1}", "graphicsDeviceType", SystemInfo.graphicsDeviceType);
            deviceInfo.AppendFormat("& {0}:{1}", "graphicsDeviceVendor", SystemInfo.graphicsDeviceVendor);
            deviceInfo.AppendFormat("& {0}:{1}", "graphicsDeviceVendorID", SystemInfo.graphicsDeviceVendorID);
            deviceInfo.AppendFormat("& {0}:{1}", "graphicsDeviceVersion", SystemInfo.graphicsDeviceVersion);
            deviceInfo.AppendFormat("& {0}:{1}", "graphicsMemorySize", SystemInfo.graphicsMemorySize);
            deviceInfo.AppendFormat("& {0}:{1}", "graphicsMultiThreaded", SystemInfo.graphicsMultiThreaded);
            deviceInfo.AppendFormat("& {0}:{1}", "graphicsShaderLevel", SystemInfo.graphicsShaderLevel);
            deviceInfo.AppendFormat("& {0}:{1}", "maxTextureSize", SystemInfo.maxTextureSize);
            deviceInfo.AppendFormat("& {0}:{1}", "npotSupport", SystemInfo.npotSupport);
            deviceInfo.AppendFormat("& {0}:{1}", "cpuCores", SystemInfo.processorCount);
            deviceInfo.AppendFormat("& {0}:{1}", "resolution", Screen.width + "*" + Screen.height);
            deviceInfo.AppendFormat("& {0}:{1}", "processorType", SystemInfo.processorType);
            NetworkServer.SendMessage(Encoding.ASCII.GetBytes(deviceInfo.ToString()), 2, width, height);
        }
 
    }

}
