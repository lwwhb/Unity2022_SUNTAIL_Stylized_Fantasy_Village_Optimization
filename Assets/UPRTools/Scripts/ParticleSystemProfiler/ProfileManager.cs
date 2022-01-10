using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;


namespace UPRProfiler
{
    #if UNITY_EDITOR
    [ExecuteAlways]
    public class ProfileManager : MonoBehaviour
    {
        public string m_ResultsPersistencePath;
        public string m_PrefabPath;
        
        private ParticleSystem[] m_ParticleSystems;
        private MethodInfo m_CalculateEffectUIDataMethod;
        private MethodInfo m_GetTextureMemorySizeMethod;
        private ProfileResults m_PersistenceProfileResults;
        private ParticleSystemCheckResult m_ParticleSystemProfileResult;
        private float m_CurrentTime;

        private void Awake()
        {
            m_PersistenceProfileResults = new ProfileResults(m_ResultsPersistencePath);
            m_ParticleSystemProfileResult = new ParticleSystemCheckResult();
            m_ParticleSystemProfileResult.ParticleSystemPath = m_PrefabPath;
        }


        private void Start()
        {

            if (Application.IsPlaying(gameObject))
            {
                m_CalculateEffectUIDataMethod = typeof(ParticleSystem).GetMethod("CalculateEffectUIData",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                m_GetTextureMemorySizeMethod = typeof(AssetDatabase).Assembly.GetType("UnityEditor.TextureUtil")
                    .GetMethod("GetStorageMemorySize", BindingFlags.Public | BindingFlags.Static);
                m_ParticleSystems = GetComponentsInChildren<ParticleSystem>();
                if (m_ParticleSystems.Length == 0)
                {
                    Destroy(gameObject);
                }

                foreach (var ps in m_ParticleSystems)
                {
                    ps.loop = false;
                }
            }

        }
        
        
        private void LateUpdate()
        {
            if (Application.IsPlaying(gameObject))
            {
                m_ParticleSystems = GetComponentsInChildren<ParticleSystem>();
                bool psPlaying = false;
                RecordDrawCall();
                RecordParticleCount();
                RecordFrameTime();
                RecordMemoryUsage();
                m_CurrentTime = Time.realtimeSinceStartup;
                foreach (var ps in m_ParticleSystems)
                {
                    if (ps.isPlaying)
                    {
                        psPlaying = true;
                    }
                }
                if (!psPlaying)
                {
                    m_PersistenceProfileResults.Append(m_ParticleSystemProfileResult);
                    m_PersistenceProfileResults.Save();
                    EditorApplication.isPlaying = false;
                }
            }
        }

        private void RecordParticleCount()
        {
            var frameCount = 0;
            foreach (var ps in m_ParticleSystems)
            {
                var count = 0;
                object[] invokeArgs = {count, 0.0f, Mathf.Infinity};
                try
                {
                    m_CalculateEffectUIDataMethod.Invoke(ps, invokeArgs);
                    count = (int) invokeArgs[0];
                }
                catch (Exception)
                {
                    count = 0;
                }
               
                frameCount += count;
            }
            m_ParticleSystemProfileResult.ParticleCounts.Add(frameCount);
        }
        

        private void RecordDrawCall()
        {
            m_ParticleSystemProfileResult.DrawCalls.Add(UnityStats.batches / 2);
        }

        private void RecordFrameTime()
        {
            m_ParticleSystemProfileResult.FrameTimes.Add(Time.realtimeSinceStartup - m_CurrentTime);
        }

        private void RecordMemoryUsage()
        {
            var textures = new HashSet<Texture>();
            int sumSize = 0;

            var rendererList = gameObject.GetComponentsInChildren<ParticleSystemRenderer>(true);

            foreach (var item in rendererList)
            {
                if (item.sharedMaterial)
                {
                    Texture texture = item.sharedMaterial.mainTexture;
                    if (texture && !textures.Contains(texture))
                    {
                        textures.Add(texture);
                        sumSize += GetTextureMemorySize(texture);
                    }
                }
            }
            m_ParticleSystemProfileResult.MemoryUsages.Add(sumSize);
        }

        private int GetTextureMemorySize(Texture texture)
        {
            object[] invokeArgs = {texture};
            return (int)m_GetTextureMemorySizeMethod.Invoke(null, invokeArgs);
        }
    }
    #endif
}
