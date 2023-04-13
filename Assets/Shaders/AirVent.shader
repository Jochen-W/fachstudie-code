// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/GlassShader"
{
	Properties
	{
        // _Color ("Color", Color) = (1,1,1,0)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}

        _TileSize ("Tile size", Float) = 0.0
        _InverseTileSize ("1 / tile_size  (for performance)", Float) = 0.0

	    _HeightMap ("Height Map", 2D) = "white" {}
	    _HeightMultiplier ("Height Multiplier", Float) = 0.01

        // [PerRendererData] _HeightLookupPoint ("Position where to sample the height from", Vector) = (0,0,0,1)
	}
	SubShader
	{
        //https://docs.unity3d.com/Manual/SL-SubShaderTags.html
        //Tags{ "RenderType"="" "DisableBatching"="" "ForceNoShadowCasting"="" "IgnoreProjector"="" "CanUseSpriteAtlas"="" "PreviewType"="" }
		Tags { "RenderType"="Opaque" "ForceNoShadowCasting" = "True"}
		LOD 100

		Pass
		{
		    //https://docs.unity3d.com/Manual/SL-SubShaderTags.html
		    //Cull Back | Front | Off
		    //ZTest (Less | Greater | LEqual | GEqual | Equal | NotEqual | Always)
		    //ZWrite On | Off
		    //Offset OffsetFactor, OffsetUnits
		    //Blend sourceBlendMode destBlendMode
            //Blend sourceBlendMode destBlendMode, alphaSourceBlendMode alphaDestBlendMode
            //BlendOp colorOp
            //BlendOp colorOp, alphaOp
            //AlphaToMask On | Off
            //ColorMask RGB | A | 0 | any combination of R, G, B, A
		    // ZWrite Off

		    //https://docs.unity3d.com/Manual/SL-PassTags.html
		    //Tags{ Lightmode="" }
		    Tags {"Lightmode"="ForwardBase"}

			CGPROGRAM
            #pragma target 4.6  // min version for tessellation

            #pragma vertex vertexPgrm  // per vertex
			#pragma fragment fragmentPgrm  // per pixel
            #pragma multi_compile_instancing // gpu instancing

			#include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            // fixed4 _Color;

            sampler2D _MainTex;
            Texture2D _HeightMap;
            SamplerState sampler_HeightMap;

            float _TileSize;
            float _InverseTileSize;

            float _HeightMultiplier;

            // gpu instancing
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _HeightLookupPoint)
            UNITY_INSTANCING_BUFFER_END(Props)


            struct VertexData {  // in local space
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID // gpu instancing
            };

            struct FragmentData {
                float4 pos : SV_POSITION;  // in world space
	            float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 worldPos : TEXCOORD2;
                // float3 view_dir : TEXCOORD3;  // vertex to camera
                UNITY_VERTEX_INPUT_INSTANCE_ID // gpu instancing
            };


            float heightFromRGB(float3 rgb_vec) {
                // from https://www.mapzen.com/blog/terrain-tile-service
                return ((rgb_vec.r * 255.0) * 256.0 + (rgb_vec.g * 255.0) + (rgb_vec.b * 255.0) / 256.0) - 32768.0;
            }

            // _InverseTileSize = 1/_TileSize
            float2 uvFromWorldPos(float3 wPos) {
                // *10.5 so we always have a positive result
                float u = fmod(wPos.x + _TileSize * 10.5, _TileSize);
                float v = fmod(wPos.z + _TileSize * 10.5, _TileSize);
                return float2(u * _InverseTileSize, v * _InverseTileSize);
            }

            FragmentData vertexPgrm(VertexData input_data) {
                FragmentData fData;

                UNITY_SETUP_INSTANCE_ID(input_data); // gpu instancing
                float height = heightFromRGB(_HeightMap.SampleLevel(sampler_HeightMap, uvFromWorldPos(UNITY_ACCESS_INSTANCED_PROP(Props, _HeightLookupPoint)), 0).rgb);
                UNITY_TRANSFER_INSTANCE_ID(input_data, fData); // gpu instancing

                fData.pos = input_data.vertex;
                fData.pos.y += height * _HeightMultiplier;

                fData.uv = input_data.texcoord;
                fData.normal = UnityObjectToWorldNormal(input_data.normal);
                // Calculate world position for face-normal calculation in fragment shader (flat shading).
				fData.worldPos = mul(unity_ObjectToWorld, input_data.vertex);
                // fData.view_dir = normalize(WorldSpaceViewDir(fData.pos));

                fData.pos = UnityObjectToClipPos(fData.pos);

                return fData;
            }


			float4 fragmentPgrm (FragmentData data) : SV_Target
			{
                UNITY_SETUP_INSTANCE_ID(data); // gpu instancing

                // renormalize (since the interpolation between vertices doesn't produce unit-vectors)
                data.normal = normalize(data.normal);
                float3 view_dir = normalize(_WorldSpaceCameraPos - data.pos);

                float3 surface_col = tex2D(_MainTex,  float2(1.0-data.uv.x, 1.0-data.uv.y) ).rgb;

                // ambient lighting (general light from anywhere), so it isn't dark without a light
                float ambient = 0.3;

                // diffuse lighting
                float diffuse = saturate(dot(data.normal, _WorldSpaceLightPos0.xyz));

                // specular lighting
                float specular_strength = 0.5;
                int specular_spot_intensity = 32;
                float3 reflected_light_dir = normalize(reflect(-_WorldSpaceLightPos0.xyz, data.normal));
                float specular = pow(max(dot(view_dir, reflected_light_dir), 0.0), specular_spot_intensity) * specular_strength;

                float3 col = surface_col * _LightColor0.xyz * min(ambient + diffuse + specular, 1);
                return float4(col, 1);
			}
			ENDCG
		}
	}
}
