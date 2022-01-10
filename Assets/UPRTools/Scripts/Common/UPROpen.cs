using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace UPRProfiler.Common
{
    public class UPROpen
    {
        /***
         * parameter
         * subjectName is the name of subject
         * groupName is the name of group，subset of subject in upr report
         * chartType now supports "line", "table" and "scatter", default "table"
         * for "scatter" data, "value" is necessary, equals the value of this point
         * data is the customizedData need to display, limited with type float, double, int, long etc...
         * example:
         *    UPROpen.SendCustomizedData("SceneSubject", "LoadSceneGroup", "line", new Dictionary<string, string){{"LoadSceneTime","0.56"}, {"LoadSceneAsset","0.23"}};        
         * return bool if upr package is not connected, data will be abort and return false. if data is sent will return true
         */
        private static StringBuilder builder = new StringBuilder();
        public static bool SendCustomizedData(String subjectName, String groupName, String chartType, Dictionary<string, string> data)
        {
            
            if (!NetworkServer.isConnected)
                return false;
            Profiler.BeginSample("Profiler.UPRCustomizeData");
#if UNITY_2018_2_OR_NEWER
            builder.Remove(0, builder.Length);
#else
            builder = new StringBuilder();
#endif

            chartType = (chartType == "") ? "table" : chartType;
            builder.Append(subjectName);
            builder.Append("|");
            builder.Append(groupName);
            builder.Append("|");
            builder.Append(chartType);
            builder.Append("|");
            builder.Append("{");
            foreach (var item in data)
            {
                builder.AppendFormat(" \"{0}\":\"{1}\",", item.Key, item.Value);
            }
            builder.Remove(builder.Length - 1, 1);
            builder.Append("}");
           // Debug.Log(builder.ToString());
            UPRMessage sample = new UPRMessage
            {
                rawBytes = Encoding.ASCII.GetBytes(builder.ToString()),
                type = (int)DataType.Customized
            };
            NetworkServer.SendMessage(sample);
            Profiler.EndSample();
            return true;
        }

        /***
         * this function for add tag in script
         * parameter
         * tagName is the name of tag, default can show the frame index         
         * example:
         *    UPROpen.AddFrameTag("TagName");
         *    UPROpen.AddFrameTag();
         * return bool if upr package is not connected, data will be abort and return false. if data is sent will return true
         */
        public static bool AddFrameTag(string tagName = "")
        {
            return sendCustomizedAction("tag", tagName);
        }
        
        /***
         * this function for add object snapshot in script
         * parameter
         * objectName is the name of object snapshot        
         * example:
         *    UPROpen.CaptureObjectSnapshot();
         * return bool if upr package is not connected, data will be abort and return false. if data is sent will return true
         */
        public static bool CaptureObjectSnapshot()
        {
            return sendCustomizedAction("object");
        }

        /***
         * this function for add memory snapshot in script (this action will consume much performance, do not call it frequently)
         * this function is used for version Unity 2019.3.15 or above. please make sure it. 
         * parameter
         * memoryName is the name of memory snapshot         
         * example:
         *    UPROpen.CaptureMemorySnapshot();
         * return bool if upr package is not connected, data will be abort and return false. if data is sent will return true
         */
        public static bool CaptureMemorySnapshot()
        {
            return sendCustomizedAction("memory");
        }

        private static bool sendCustomizedAction(string action, string actionName = "")
        {
            if (!NetworkServer.isConnected)
                return false;
            Profiler.BeginSample("Profiler.UPRCustomizedAction");
            UPRMessage sample = new UPRMessage
            {
                rawBytes = Encoding.ASCII.GetBytes(string.Format("{{{0}|{1}}}", action, actionName)),
                type = (int)DataType.CustomizedAction
            };
            NetworkServer.SendMessage(sample);
            Profiler.EndSample();
            return true;
        }
    }
}