using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UPRProfiler.Common;

namespace UPRProfiler
{
    [Serializable]
    public class ShaderVariantsCheck
    {
        public string Name;
        public string ProjectId;
        public List<ShaderVariantsCheckResult> ShaderVariantsCheckUploadResults;
        public List<string> PropertyUploadList;
    }

    public class ShaderVariantsCheckUploadResult
    {
        public string ShaderVariantsCheckId;
    }

    [Serializable]
    public class ShaderVariantsCheckResult
    {
        public string ShaderName;
        public string ShaderPlatform;
        public string ShaderTier;
        public string ShaderPassName;
        public string ShaderKeywords;

        public ShaderVariantsCheckResult(string shaderName, string shaderPlatform, string shaderTier,
            string shaderPassName,
            string shaderKeywords)
        {
            ShaderName = shaderName;
            ShaderPlatform = shaderPlatform;
            ShaderTier = shaderTier;
            ShaderPassName = shaderPassName;
            ShaderKeywords = shaderKeywords;
        }
    }

#if UNITY_2018_2_OR_NEWER
    public class ShadersVariantsCheckManager
        : IPreprocessShaders, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        static HashSet<string> s_ShaderNames;
        static Dictionary<string, List<ShaderVariantsCheckResult>> s_ShaderReports;

        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!UPRToolSetting.Instance.enableShaderVariantsCheck)
            {
                return;
            }

            s_ShaderNames = new HashSet<string>();
            s_ShaderReports = new Dictionary<string, List<ShaderVariantsCheckResult>>();

            var shaderGuids = AssetDatabase.FindAssets("t:shader");
            foreach (var guid in shaderGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadMainAssetAtPath(assetPath) as Shader;
                if (shader != null)
                {
                    s_ShaderNames.Add(shader.name);
                }
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!UPRToolSetting.Instance.enableShaderVariantsCheck)
            {
                return;
            }

            if (s_ShaderReports == null)
            {
                Debug.Log("No shader variants found");
            }
            else
            {
                Debug.Log("Start uploading shader variants report");
                var result = new ShaderVariantsCheck
                {
                    PropertyUploadList = new List<string>(),
                    ShaderVariantsCheckUploadResults = new List<ShaderVariantsCheckResult>()
                };
                result.PropertyUploadList.Add("Platform");
                result.PropertyUploadList.Add(report.summary.platform.ToString());
                foreach (var sv in s_ShaderReports.Values)
                {
                    result.ShaderVariantsCheckUploadResults.AddRange(sv);
                }

                UploadResults(result);
            }
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!UPRToolSetting.Instance.enableShaderVariantsCheck)
            {
                return;
            }

            if (snippet.shaderType != ShaderType.Fragment)
                return;

            var shaderName = shader.name;

            if (!s_ShaderNames.Contains(shaderName)) return;
            if (!s_ShaderReports.ContainsKey(shaderName))
            {
                s_ShaderReports.Add(shaderName, new List<ShaderVariantsCheckResult>());
            }

            foreach (var shaderCompilerData in data)
            {
                var shaderKeywordSet = shaderCompilerData.shaderKeywordSet.GetShaderKeywords().ToArray();

#if UNITY_2019_3_OR_NEWER
                var keywords = shaderKeywordSet.Select(keyword =>
                    ShaderKeyword.IsKeywordLocal(keyword)
                        ? ShaderKeyword.GetKeywordName(shader, keyword)
                        : ShaderKeyword.GetGlobalKeywordName(keyword)).ToArray();
#else
                        var keywords = shaderKeywordSet.Select(keyword => keyword.GetKeywordName()).ToArray();
#endif
                var keywordString = string.Join(", ", keywords);
                if (string.IsNullOrEmpty(keywordString))
                    keywordString = "<no keywords>";

                s_ShaderReports[shaderName].Add(new ShaderVariantsCheckResult(shaderName,
                    shaderCompilerData.shaderCompilerPlatform.ToString(),
                    shaderCompilerData.graphicsTier.ToString(), snippet.passName,
                    keywordString));
            }
        }

        private static void UploadResults(ShaderVariantsCheck result)
        {
            result.ProjectId = UPRToolSetting.Instance.projectId;
            var json = JsonUtility.ToJson(result);
            if (UPRToolSetting.Instance.projectId == "" || !isGuid(UPRToolSetting.Instance.projectId))
            {
                UploadFailed(json, "ProjectId not specified or not valid");
                return;
            }

            var uploadUrl = Utils.UploadHost + "/shader-variants-check";
            var client = new WebClient {Headers = {[HttpRequestHeader.ContentType] = "application/json"}};
            try
            {
                var resp = client.UploadString(uploadUrl, json);
                var res = JsonUtility.FromJson<ShaderVariantsCheckUploadResult>(resp);
                Debug.Log("You can see the shader variants report at: " + Utils.BrowserHost + "/shader-variants-check/" + res.ShaderVariantsCheckId);
            }
            catch (Exception e)
            {
                Debug.Log("Error uploading shader variants report: " + e);
                UploadFailed(json, "ShaderVariantsReportResult Upload failed");
            }
        }

        private static void UploadFailed(string json, string msg)
        {
            var resultFileName = "Library/shaderVariantsReportResult.json";
            File.WriteAllText(resultFileName, json);
            Debug.Log(msg + ", you can upload " + resultFileName + " manually.");
        }
        
        private static bool isGuid(string str)
        {
            Match m = Regex.Match(str, @"^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                new Guid(str);
                return true;
            }

            return false;
        }
    }
#endif
}