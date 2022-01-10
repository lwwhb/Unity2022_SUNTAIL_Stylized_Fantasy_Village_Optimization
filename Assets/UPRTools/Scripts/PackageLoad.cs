using UnityEditor;
using UnityEngine;
using UPRLuaProfiler;
using UPRProfiler.Common;

namespace UPRProfiler
{
    public class PackageLoad : MonoBehaviour
    {
        public static bool useLua = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void OnStartGame()
        {
            
#if !UNITY_EDITOR
    #if NO_PACKAGE
            return;
    #endif    

#if UNITY_2018_2_OR_NEWER
            var uprOverdrawMonitor = OverdrawMonitor.UPROverdrawMonitor.Instance;
#endif
            GameObject uprGameObject = new GameObject("UPRGameObject");
            uprGameObject.name = "UPRProfiler";
            uprGameObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(uprGameObject);
            uprGameObject.AddComponent<InnerPackageS>();
            NetworkServer.ConnectTcpPort(56000);
            Application.wantsToQuit += Quit;
#endif
        }
        
        static bool Quit()
        {
            NetworkServer.Close();
#if UPR_LUA_PROFILER
            NetWorkClient.Close();
#endif
            return true;
        }
    }
}

