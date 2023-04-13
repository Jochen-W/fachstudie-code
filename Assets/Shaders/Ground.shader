// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Ground"
{
	Properties
	{
	    _MainTex ("Texture", 2D) = "white" {}
	    _NormalMap ("Normal Map", 2D) = "white" {}
	    _HeightMap ("Height Map", 2D) = "white" {}

        // for seamless overlaps (use variables need to be set if the texture loading was successful)
        _HeightMapRight ("Height Map (Right)", 2D) = "white" {}
        _HeightMapBelow ("Height Map (Below)", 2D) = "white" {}
        _UseRight ("Wether to sample form _HeightMapRight", Range(0,1)) = 0  // false
        _UseBelow ("Wether to sample form _HeightMapBelow", Range(0,1)) = 0  // false

	    _HeightMultiplier ("Height Multiplier", Float) = 0.01

	    _TessDistance ("Distance to tessellate in (with max tessellation)", Float) = 5.0
	    _TessDistanceFade ("Distance to fade the tessellate in (fades from value above until this value linearly)", Float) = 10.0
	    _TessFactor ("Tessellation Factor", Float) = 4.0
	}
	SubShader
	{
        //https://docs.unity3d.com/Manual/SL-SubShaderTags.html
        //Tags{ "RenderType"="" "DisableBatching"="" "ForceNoShadowCasting"="" "IgnoreProjector"="" "CanUseSpriteAtlas"="" "PreviewType"="" }
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "ForceNoShadowCasting"="true"}
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
			#pragma hull hullPgrm  // per triangle
            #pragma domain domainPgrm // per old and new vertex
			// #pragma geometry geometryPgrm  // per vertex
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

            sampler2D _MainTex;
            Texture2D _NormalMap;
            Texture2D _HeightMap;
            Texture2D _HeightMapRight;
            Texture2D _HeightMapBelow;
            SamplerState sampler_NormalMap;
            SamplerState sampler_HeightMap;
            SamplerState sampler_HeightMapRight;
            SamplerState sampler_HeightMapBelow;
            float _UseRight;
            float _UseBelow;

            float _HeightMultiplier;
            float _TessDistance;
            float _TessDistanceFade;
            float _TessFactor;

            struct VertexData {  // in local space
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float3 normal : NORMAL;
                float3 tangent : TANGENT;
            };

            struct TessellationData {
                float4 vertex : INTERNALTESSPOS;  // other semantic, but same value
                float2 texcoord : TEXCOORD0;
                float3 normal : NORMAL;
                float3 tangent : TANGENT;
            };
            struct TessellationFactors {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            struct FragmentData {
                float4 pos : SV_POSITION;  // in world space
	            float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 tangent : TEXCOORD2;
                float3 view_dir : TEXCOORD3;  // vertex to camera
            };

            // float mapValue(float n, float start1, float stop1, float start2, float stop2) {
            //     return ((n-start1)/(stop1-start1))*(stop2-start2)+start2;
            // };

            float heightFromRGB(float3 rgb_vec) {
                // from https://www.mapzen.com/blog/terrain-tile-service
                // - 11934.0 (Mariana Trench)
                return ((rgb_vec.r * 255.0) * 256.0 + (rgb_vec.g * 255.0) + (rgb_vec.b * 255.0) / 256.0) - 32768.0;
            }

            TessellationData vertexPgrm(VertexData input_data) {
                TessellationData td;
                td.vertex = input_data.vertex;
                // lookup height for camera-pos-distance
                float height = heightFromRGB(_HeightMap.SampleLevel(sampler_HeightMap, float2(1 - input_data.texcoord.x, 1 - input_data.texcoord.y), 0).rgb);

                // for seamless overlaps
                if(_UseRight == 1 && input_data.texcoord.x == 0){
                    height = heightFromRGB(_HeightMapRight.SampleLevel(sampler_HeightMapRight, float2(input_data.texcoord.x, 1-input_data.texcoord.y), 0).rgb);
                }
                if(_UseBelow == 1 && input_data.texcoord.y == 1){
                    height = heightFromRGB(_HeightMapBelow.SampleLevel(sampler_HeightMapBelow, float2(1 - input_data.texcoord.x, input_data.texcoord.y), 0).rgb);
                }

                td.vertex.y = height * _HeightMultiplier;
                td.tangent = input_data.tangent;
                td.normal = input_data.normal;
                td.texcoord = input_data.texcoord;
                return td;
            }


            TessellationFactors MyPatchConstantFunction (InputPatch<TessellationData, 3> triangle_patch) {
                float4 dist_based_tess = UnityDistanceBasedTess(triangle_patch[0].vertex, triangle_patch[1].vertex, triangle_patch[2].vertex, _TessDistance, _TessDistanceFade, _TessFactor);
                TessellationFactors f;
                f.edge[0] = dist_based_tess.x;
                f.edge[1] = dist_based_tess.y;
                f.edge[2] = dist_based_tess.z;
                f.inside = dist_based_tess.w;
                return f;
            }

            [UNITY_domain("tri")]
            [UNITY_outputcontrolpoints(3)]
            [UNITY_outputtopology("triangle_cw")]
            [UNITY_partitioning("integer")]
            [UNITY_patchconstantfunc("MyPatchConstantFunction")]
            TessellationData hullPgrm(InputPatch<TessellationData, 3> triangle_patch, uint id : SV_OutputControlPointID)
            {
                return triangle_patch[id];
            }


            [UNITY_domain("tri")]
            FragmentData domainPgrm(TessellationFactors factors, OutputPatch<TessellationData, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                VertexData data;

                // interpolate macro
                #define MY_DOMAIN_PROGRAM_INTERPOLATE(fieldName) data.fieldName = \
                    patch[0].fieldName * barycentricCoordinates.x + \
                    patch[1].fieldName * barycentricCoordinates.y + \
                    patch[2].fieldName * barycentricCoordinates.z;


                MY_DOMAIN_PROGRAM_INTERPOLATE(vertex);
                MY_DOMAIN_PROGRAM_INTERPOLATE(normal);
                MY_DOMAIN_PROGRAM_INTERPOLATE(tangent);
                MY_DOMAIN_PROGRAM_INTERPOLATE(texcoord);

                FragmentData fData;
                // TODO: lookup height (red * 256 + green + blue / 256) - 32768
                // float3 colors = _NormalAndHeightMap.SampleLevel(sampler_NormalAndHeightMap, data.texcoord, 0).rgb;
                // data.vertex.y = (colors.r * 256.0 * 256.0 + colors.g * 256.0 + colors.b) - 32768.0;
                // original uv (0,0) is at bottom left -> shift by (0,1)-uv

                // sample normals and height
                float3 normals = _NormalMap.SampleLevel(sampler_NormalMap, float2(1.0 - data.texcoord.x, 1.0 - data.texcoord.y), 0).rgb * 2.0 - 1.0;
                float height = heightFromRGB(_HeightMap.SampleLevel(sampler_HeightMap, float2(1 - data.texcoord.x, 1 - data.texcoord.y), 0).rgb);
                // for seamless overlaps
                if(_UseRight == 1 && data.texcoord.x == 0){
                    height = heightFromRGB(_HeightMapRight.SampleLevel(sampler_HeightMapRight, float2(data.texcoord.x, 1 - data.texcoord.y), 0).rgb);
                }
                if(_UseBelow == 1 && data.texcoord.y == 1){
                    height = heightFromRGB(_HeightMapBelow.SampleLevel(sampler_HeightMapBelow, float2(1 - data.texcoord.x, data.texcoord.y), 0).rgb);
                }

                data.vertex.y = height * _HeightMultiplier;

                fData.pos = UnityObjectToClipPos(data.vertex);
                fData.uv = data.texcoord;
                fData.normal = UnityObjectToWorldNormal(data.normal);
                fData.tangent = UnityObjectToWorldNormal(data.tangent);
                fData.view_dir = normalize(WorldSpaceViewDir(data.vertex));

                return fData;
            }

			float4 fragmentPgrm (FragmentData data) : SV_Target
			{
                // get color of surface
                float3 surface_col = tex2D(_MainTex,  float2(1.0-data.uv.x, 1.0-data.uv.y) ).xyz;

                // get normal of surface
                float3 normals = _NormalMap.SampleLevel(sampler_NormalMap, float2(1.0-data.uv.x, 1.0-data.uv.y), 0).rgb;
                normals = float3(1.0 - normals.r, 1.0 - normals.g, normals.b) * 2.0 - 1.0;
                float3 bi_tangent = cross(data.tangent, data.normal);
                // real_normal is the normal in world-space
                float3 real_normal = normalize(normals.r * data.tangent + normals.g * bi_tangent + normals.b * data.normal);

                // ambient lighting (general light from anywhere), so it isn't dark without a light
                float ambient = 0.2;

                // diffuse lighting
                float diffuse = 0.9 * saturate(dot(real_normal, _WorldSpaceLightPos0.xyz));

                // specular lighting
                float specular_strength = 0.5;
                int specular_spot_intensity = 32;
                float3 reflected_light_dir = normalize(reflect(_WorldSpaceLightPos0.xyz, real_normal));
                float specular = pow(max(dot(data.view_dir, reflected_light_dir), 0.0), specular_spot_intensity) * specular_strength;

                // add all together
                float3 col = surface_col * _LightColor0.xyz * min(ambient + diffuse + specular, 1);
                return float4(col.r, col.g, col.b, 1);
			}
			ENDCG
		}
	}
}