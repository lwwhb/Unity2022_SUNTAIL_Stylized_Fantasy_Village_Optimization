using System;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace UPRProfiler.Common
{
    public class Utils
    {
        public static string PackageVersion = "0.10.2";
        private static string resourceUrl = "";
        
        public static string ResourceUrl
        {
            get
            {
#if UNITY_EDITOR
                if (resourceUrl == "")
                    resourceUrl = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("UPRParticlePlay t:Scene")[0]).Replace("UPRParticlePlay.unity", "");
#endif
                return resourceUrl;
            }
        }
        
        private static string TryGetCloudProjectSettings(string propName)
        {
            var cloudProjectSettingsType = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                from type in assembly.GetTypes()
                where type.Name == "CloudProjectSettings" && type.GetProperties().Any(m => m.Name == propName)
                select type).FirstOrDefault();
            
            PropertyInfo field = cloudProjectSettingsType.GetProperty(propName);
            
            
            return field == null
                ? null
                : field.GetValue(Activator.CreateInstance(cloudProjectSettingsType), null).ToString();
        }
        
        public static string GetProjectId()
        {
            var projectId = UPRToolSetting.Instance.projectId;
            
            return string.IsNullOrEmpty(projectId) ? TryGetCloudProjectSettings("projectId") : projectId;
        }
        
        public static string UploadHost
        {
            get 
            {
                if (string.IsNullOrEmpty(UPRToolSetting.Instance.customizedServer))
                {
                    return "https://upr.unity.cn/api";
                }
                else
                {
                    return "http://" + UPRToolSetting.Instance.customizedServer + "/api";
                }
            }
        }

        public static string BrowserHost
        {
            get 
            {
                if (string.IsNullOrEmpty(UPRToolSetting.Instance.customizedServer))
                {
                    return "https://upr.unity.cn";
                }
                else
                {
                    return "http://" + UPRToolSetting.Instance.customizedServer;
                }
            }
        }
    }
}