using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UPRTools.Editor
{

    [Serializable]
    internal class ParticleSystemElement : TreeElement
    {
        public int    index;
        public string path;
        public Texture2D icon;
        public string displayName;
        public bool   enabled;
        public float  duration;
        
        public ParticleSystemElement()
        {
            
        }
        
        public ParticleSystemElement (string path, string name, int depth, int id) : base (name, depth, id)
        {
            this.index = id;
            this.path = path;
            GameObject o = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (o != null)
            {
                this.icon = AssetDatabase.GetCachedIcon(path) as Texture2D;
                this.displayName = o.name;
                var longestDuration = 0f;
                foreach (var ps in o.GetComponentsInChildren<ParticleSystem>())
                {
                    if (ps.main.duration > longestDuration)
                    {
                        longestDuration = ps.main.duration;
                    }
                }
                this.duration = longestDuration;
            }
            
            this.enabled = true;
        }
    }
    
    public class ParticleSystemElementsAsset : ScriptableObject
    {
        [SerializeField] List<ParticleSystemElement> m_TreeElements = new List<ParticleSystemElement> ();

        internal List<ParticleSystemElement> treeElements
        {
            get { return m_TreeElements; }
            set { m_TreeElements = value; }
        }
        
    }
}