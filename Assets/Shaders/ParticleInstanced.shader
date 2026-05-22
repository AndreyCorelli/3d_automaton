// URP forward-lit shader for GPU-buffer-driven instanced particle rendering.
// Reads particle state and class colors directly from StructuredBuffers using SV_InstanceID.
// Compatible with Graphics.DrawMeshInstancedIndirect.

Shader "ParticleAutomaton/ParticleInstanced"
{
    Properties
    {
        _ParticleRadius ("Particle Radius", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ── Forward Lit pass ─────────────────────────────────────────────────
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Structs ──────────────────────────────────────────────────────

            struct Particle
            {
                float3 position;
                float  weight;
                float3 velocity;
                uint   classId;
            };

            StructuredBuffer<Particle> _Particles;
            StructuredBuffer<float4>   _ClassColors;

            float _ParticleRadius;
            int   _ClassCount;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float4 color      : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            // ── Vertex ───────────────────────────────────────────────────────

            Varyings vert(Attributes IN)
            {
                Particle p  = _Particles[IN.instanceID];
                float4 col  = _ClassColors[p.classId % (uint)_ClassCount];

                // Scale unit mesh, then translate to particle world position.
                // No rotation — sphere normals survive uniform scale unchanged.
                float3 worldPos = IN.positionOS * _ParticleRadius + p.position;

                Varyings OUT;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.normalWS   = normalize(IN.normalOS);   // valid for sphere with identity rotation
                OUT.color      = col;
                OUT.positionWS = worldPos;
                return OUT;
            }

            // ── Fragment ─────────────────────────────────────────────────────

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS         = IN.positionWS;
                inputData.normalWS           = normalWS;
                inputData.viewDirectionWS    = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord        = float4(0, 0, 0, 1);
                inputData.fogCoord           = 0;
                inputData.vertexLighting     = half3(0, 0, 0);
                inputData.bakedGI            = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = float2(0, 0);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo      = IN.color.rgb;
                surface.metallic    = 0;
                surface.smoothness  = 0.3;
                surface.occlusion   = 1;
                surface.alpha       = 1;

                return UniversalFragmentPBR(inputData, surface);
            }
            ENDHLSL
        }

        // ── Shadow Caster pass ───────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Particle
            {
                float3 position;
                float  weight;
                float3 velocity;
                uint   classId;
            };

            StructuredBuffer<Particle> _Particles;
            float _ParticleRadius;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings shadowVert(Attributes IN)
            {
                Particle p      = _Particles[IN.instanceID];
                float3 worldPos = IN.positionOS * _ParticleRadius + p.position;

                Varyings OUT;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                return OUT;
            }

            half4 shadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
