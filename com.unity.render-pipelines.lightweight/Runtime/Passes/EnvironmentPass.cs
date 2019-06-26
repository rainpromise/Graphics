using System.Runtime.CompilerServices;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.LWRP
{
    internal class EnvironmentPass : ScriptableRenderPass
    {
        RenderTextureDescriptor m_Descriptor;
        RenderTargetHandle m_Source;
        RenderTargetHandle m_Destination;
        RenderTargetHandle m_Depth;
        RenderTargetHandle m_InternalLut;

        const string k_SetupEnvironmentTag = "Setup Environment Effects";

        // Builtin effects settings
        Fog m_Fog;

        public EnvironmentPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var desc = cameraTextureDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            cmd.GetTemporaryRT(m_Destination.id, desc, FilterMode.Point);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Start by pre-fetching all builtin effect settings we need
            var stack = VolumeManager.instance.stack;
            m_Fog        = stack.GetComponent<Fog>();

            var cmd = CommandBufferPool.Get(k_SetupEnvironmentTag);

            // Do Fog stuff
            using (new ProfilingSample(cmd, "Fog Setup"))
            {
                DoFogSetup();
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        #region Fog

        void DoFogSetup()
        {
            var fogParams = default(Vector4);
            
            switch (m_Fog.type.value)
            {
                case FogType.Linear:
                    fogParams = new Vector4(m_Fog.density.value, 0, m_Fog.nearFog.value, m_Fog.farFog.value);
                    Shader.EnableKeyword("FOG_LINEAR");
                    Shader.DisableKeyword("FOG_EXP2");
                    Shader.DisableKeyword("FOG_EXP");
                    break;
                case FogType.Exp2:
                    fogParams = new Vector4(m_Fog.density.value, 0, m_Fog.nearFog.value, m_Fog.farFog.value);
                    Shader.DisableKeyword("FOG_LINEAR");
                    Shader.EnableKeyword("FOG_EXP2");
                    Shader.DisableKeyword("FOG_EXP");
                    break;
                case FogType.Height:
                    fogParams = new Vector4(m_Fog.height.value, m_Fog.heightFalloff.value, m_Fog.distanceOffset.value, m_Fog.distanceFalloff.value);
                    Shader.DisableKeyword("FOG_LINEAR");
                    Shader.DisableKeyword("FOG_EXP2");
                    Shader.EnableKeyword("FOG_EXP");
                    break;
            }

            switch (m_Fog.colorType.value)
            {
                case FogColorType.Color:
                    Shader.DisableKeyword("FOGMAP");
                    break;
                case FogColorType.Gradient:
                    Shader.DisableKeyword("FOGMAP");
                    break;
                case FogColorType.CubeMap:
                    Shader.EnableKeyword("FOGMAP");
                    Shader.SetGlobalTexture("_FogMap", m_Fog.cubemap.value);
                    break;
            }

            Shader.SetGlobalColor(ShaderConstants._FogColor, m_Fog.fogColor.value);
            Shader.SetGlobalVector(ShaderConstants._FogParams, fogParams);
        }

        #endregion

        #region Internal utilities

        // Precomputed shader ids to same some CPU cycles (mostly affects mobile)
        static class ShaderConstants
        {
            public static readonly int _FogColor = Shader.PropertyToID("_FogColor");
            public static readonly int _FogParams = Shader.PropertyToID("_FogParams");
        }

        #endregion
    }
}
