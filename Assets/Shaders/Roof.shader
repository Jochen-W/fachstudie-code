// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Roof"
{
	Properties
	{

        // _Color ("Color", Color) = (1,1,1,0)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _MainTexFlat ("Albedo (RGB)", 2D) = "white" {}
        // _Glossiness ("Smoothness", Range(0,1)) = 0.5
        // _Metallic ("Metallic", Range(0,1)) = 0.0
        _UseFlatShading ("Use flat-shading", Range(0,1)) = 1.0

        _TileSize ("Tile size", Float) = 0.0
        _InverseTileSize ("1 / tile_size  (for performance)", Float) = 0.0

	    _HeightMap ("Height Map", 2D) = "white" {}
	    _HeightMultiplier ("Height Multiplier", Float) = 0.01
	}
	SubShader
	{
        //https://docs.unity3d.com/Manual/SL-SubShaderTags.html
        //Tags{ "RenderType"="" "DisableBatching"="" "ForceNoShadowCasting"="" "IgnoreProjector"="" "CanUseSpriteAtlas"="" "PreviewType"="" }
		Tags { "RenderType"="Opaque"}
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
            // #pragma geometry geometryPgrm  // per triangle
			#pragma fragment fragmentPgrm  // per pixel
			// make fog work
			//#pragma multi_compile_fog

			#include "UnityCG.cginc"
            //CG keywords
            //http://developer.download.nvidia.com/CgTutorial/cg_tutorial_appendix_d.html
            //CG library functions:
            //http://developer.download.nvidia.com/CgTutorial/cg_tutorial_appendix_e.html
            #include "Tessellation.cginc"  // usefull tessellation factor functions
            #include "UnityLightingCommon.cginc"

            // fixed4 _Color;

            sampler2D _MainTex;
            sampler2D _MainTexFlat;
            Texture2D _HeightMap;
            SamplerState sampler_HeightMap;

            float _HeightMultiplier;

            float _TileSize;
            float _InverseTileSize;

            float _UseFlatShading;

            struct VertexData {  // in local space
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;  // z = isFloat
                float3 normal : NORMAL;
                float4 color : COLOR0;  // alpha = isColorFromData
            };

            struct FragmentData {
                float4 pos : SV_POSITION;  // in world space
	            float3 uv : TEXCOORD0;  // z = isFloat
                float3 normal : TEXCOORD1;
                float4 worldPos : TEXCOORD2;
                float4 color : TEXCOORD3;
            };

            // float mapValue(float n, float start1, float stop1, float start2, float stop2) {
            //     return ((n-start1)/(stop1-start1))*(stop2-start2)+start2;
            // };

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

            // from https://www.ronja-tutorials.com/post/024-white-noise/
            float rand3dTo1d(float3 value, float3 dotDir = float3(12.9898, 78.233, 37.719)){
                float3 smallValue = sin(value);
                float random = dot(smallValue, dotDir);
                random = frac(sin(random) * 143758.5453);
                return random;
            }
            float3 rand3dTo3d(float3 value){
                return float3(
                    rand3dTo1d(value, float3(39.346, 11.135, 83.155)),
                    rand3dTo1d(value, float3(73.156, 52.235, 09.151)),
                    rand3dTo1d(value, float3(12.989, 78.233, 37.719))
                );
            }

            float3 rgb2grey(float3 c)
            {
                // only generates lighter grays
                float gray = (c.r * 0.299 + c.g * 0.587 + c.b * 0.114) * 0.5 + 0.5;
                return float3(gray, gray, gray);
            }

            FragmentData vertexPgrm(VertexData input_data) {
                FragmentData fData;

                float height = heightFromRGB(_HeightMap.SampleLevel(sampler_HeightMap, uvFromWorldPos(mul(unity_ObjectToWorld, float4(0,0,0,1))), 0).rgb);

                fData.pos = input_data.vertex;
                fData.pos.y += height * _HeightMultiplier;

                fData.uv = input_data.texcoord;
                fData.normal = UnityObjectToWorldNormal(input_data.normal);
                // Calculate world position for face-normal calculation in fragment shader (flat shading).
				fData.worldPos = mul(unity_ObjectToWorld, input_data.vertex);
                fData.color = input_data.color;

                fData.pos = UnityObjectToClipPos(fData.pos);

                return fData;
            }

            // [maxvertexcount(3)]
            // void geometryPgrm(triangle FragmentData data[3], inout TriangleStream<FragmentData> stream)
            // {
            //     data[0].bary = float3(1, 0, 0);
            //     data[1].bary = float3(0, 1, 0);
            //     data[2].bary = float3(0, 0, 1);
            //     stream.Append(data[0]);
            //     stream.Append(data[1]);
            //     stream.Append(data[2]);
            // }

			float4 fragmentPgrm (FragmentData data) : SV_Target
			{
                // renormalize (since the interpolation between vertices doesn't produce unit-vectors)
                data.normal = normalize(data.normal);
                float3 view_dir = normalize(_WorldSpaceCameraPos - data.pos);


                // FLAT SHADING
                float3 flatNormal = normalize(cross(ddy(data.worldPos), ddx(data.worldPos)));
                data.normal = lerp(data.normal, flatNormal, _UseFlatShading);  // lerp insted of if

                float3 rand = rand3dTo3d(mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz);

                float4 tex_col = lerp(
                    tex2D(_MainTex,  float2(1.0-data.uv.x, 1.0-data.uv.y)),
                    tex2D(_MainTexFlat,  float2((1.0-data.uv.x) * 0.2f, (1.0-data.uv.y) * 0.2f)),
                    data.uv.z  // isFlat
                );
                float3 surface_col = lerp(
                    lerp(  // random color variations
                        tex_col.rgb * lerp(rgb2grey(rand), rand, 0.2),
                        tex_col.rgb * lerp(rgb2grey(rand), rand, 0.05),
                        data.uv.z),  // isFlat
                    tex_col.rgb * data.color.rgb,
                    data.color.a  // isColorFromData
                );

                // ambient lighting (general light from anywhere), so it isn't dark without a light
                float ambient = 0.3;

                // diffuse lighting
                float diffuse = 0.8 * saturate(dot(data.normal, _WorldSpaceLightPos0.xyz));

                // specular lighting
                float specular_strength = 0.5;
                int specular_spot_intensity = 32;
                float3 reflected_light_dir = normalize(reflect(_WorldSpaceLightPos0.xyz, data.normal));
                float specular = pow(max(dot(view_dir, reflected_light_dir), 0.0), specular_spot_intensity) * specular_strength;

                // add all together
                float3 col = surface_col * _LightColor0.xyz * min(ambient + diffuse + specular, 1);
                return float4(col.r, col.g, col.b, 1);
			}
			ENDCG
		}
	}
}
