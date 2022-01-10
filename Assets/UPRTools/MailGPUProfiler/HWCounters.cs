using System.Runtime.InteropServices;

namespace UPRProfiler.Mail
{
    public static class HWCPipe
    {
        public enum CpuCounter
        {
            Cycles = 0,
            Instructions,
            CacheReferences,
            CacheMisses,
            BranchInstructions,
            BranchMisses,
            MaxValue
        }
        
        public enum GpuCounter
        {
            GpuCycles,
            VertexComputeCycles,
            FragmentCycles,
            TilerCycles,

            VertexComputeJobs,
            FragmentJobs,
            Pixels,

            Tiles,
            EarlyZTests,
            EarlyZKilled,
            LateZTests,
            LateZKilled,

            Instructions,
            DivergedInstructions,

            ShaderCycles,
            ShaderArithmeticCycles,
            ShaderLoadStoreCycles,
            ShaderTextureCycles,

            CacheReadLookups,
            CacheWriteLookups,
            ExternalMemoryReadAccesses,
            ExternalMemoryWriteAccesses,
            ExternalMemoryReadStalls,
            ExternalMemoryWriteStalls,
            ExternalMemoryReadBytes,
            ExternalMemoryWriteBytes,

            MaxValue
        }

#if UNITY_ANDROID        
        [DllImport("hwcpipe")]
        public static extern void Start();

        [DllImport("hwcpipe")]
        public static extern void Stop();

        [DllImport("hwcpipe")]
        public static extern void Sample();


        [DllImport("hwcpipe")]
        public static extern int CPU_GetNumCounters();

        [DllImport("hwcpipe")]
        public static extern bool CPU_IsCounterSupported(int counterId);

        [DllImport("hwcpipe")]
        public static extern void CPU_EnableCounter(int counterId);

        [DllImport("hwcpipe")]
        public static extern int CPU_GetCounterValue(int counterId);


        [DllImport("hwcpipe")]
        public static extern int GPU_GetNumCounters();

        [DllImport("hwcpipe")]
        public static extern bool GPU_IsCounterSupported(int counterId);

        [DllImport("hwcpipe")]
        public static extern void GPU_EnableCounter(int counterId);

        [DllImport("hwcpipe")]
        public static extern int GPU_GetCounterValue(int counterId);
#else
        public static void Start()
        {
        }

        public static void Stop()
        {
        }

        public static void Sample()
        {
        }


        public static int CPU_GetNumCounters()
        {
            return (int)CpuCounter.MaxValue;
        }


        public static bool CPU_IsCounterSupported(int counterId)
        {
            return false;
        }

        public static void CPU_EnableCounter(int counterId)
        {
        }

        public static int CPU_GetCounterValue(int counterId)
        {
            return -1;
        }


        public static int GPU_GetNumCounters()
        {
            return (int)GpuCounter.MaxValue;
        }

        public static bool GPU_IsCounterSupported(int counterId)
        {
            return false;
        }

        public static void GPU_EnableCounter(int counterId)
        {
        }

        public static int GPU_GetCounterValue(int counterId)
        {
            return -1;
        }
#endif
    }
}
