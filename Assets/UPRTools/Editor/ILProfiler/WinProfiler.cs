using System.IO;
using UPRLuaProfiler;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace UPRProfiler
{
    public class WinProfiler 
    {
        public static ScriptingImplementation scriptBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
#if UNITY_2017_1_OR_NEWER
        [PostProcessBuildAttribute(1)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            var setting = UPRToolSetting.Instance;
            
            if (scriptBackend == ScriptingImplementation.Mono2x && (target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64))
            {

                string AssemblyPath = pathToBuiltProject.Replace(".exe", "_Data/Managed/");

                if (!Directory.Exists(AssemblyPath))
                {
                    return;
                }
                
                if (setting.enableMonoProfiler)
                {
                    InjectMethods.InjectAllMethods(AssemblyPath + "Assembly-CSharp.dll");
                    Debug.Log("Listening Mono Profiler Success");
                }

                if (setting.loadScene)
                {
                    InjectUtils.addProfiler(AssemblyPath, "UnityEngine.CoreModule.dll", "SceneManager", "LoadScene");
                }
                if (setting.loadAsset)
                {
                    InjectUtils.addProfiler(AssemblyPath, "UnityEngine.AssetBundleModule.dll", "AssetBundle", "LoadAsset");
                    InjectUtils.addProfiler(AssemblyPath, "UnityEngine.AssetBundleModule.dll", "AssetBundle", "Load");
                }
                if (setting.loadAssetBundle)
                {
                    InjectUtils.addProfiler(AssemblyPath, "UnityEngine.AssetBundleModule.dll", "AssetBundle", "LoadFromFile");
                }
                if (setting.instantiate)
                {
                    InjectUtils.addProfiler(AssemblyPath, "UnityEngine.CoreModule.dll", "Object", "Instantiate");
                }
                
            }
        }
#endif
    }
}

