// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Building"
{
	Properties
	{

        // Texture2DArray: s. https://www.youtube.com/watch?v=Q60cdwZDyjE&ab_channel=Holistic3D, https://medium.com/@calebfaith/how-to-use-texture-arrays-in-unity-a830ae04c98b
        // _Color ("Color", Color) = (1,1,1,0)
        _MainTex ("Window Mask", 2DArray) = "white" {}
        _MainTexPaired ("Window Mask (paired)", 2DArray) = "white" {}
        _NormalMap ("Window normal map", 2DArray) = "white" {}
        _NormalMapPaired ("Window normal map (paired)", 2DArray) = "white" {}
        _MainTexDoor ("Door Mask", 2DArray) = "white" {}
        _NormalMapDoor ("Door normal map", 2DArray) = "white" {}
        _StairTex ("Stair texture", 2D) = "white" {}

        // _DensityTex ("Density texture", 2D) = "white" {}

        _UseFlatShading ("Use flat-shading", Range(0,1)) = 1.0

        _TileSize ("Tile size", Float) = 0.0
        _InverseTileSize ("1 / tile_size  (for performance)", Float) = 0.0

        // TODO: add density-texture
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

            //CG keywords
            //http://developer.download.nvidia.com/CgTutorial/cg_tutorial_appendix_d.html
            //CG library functions:
            //http://developer.download.nvidia.com/CgTutorial/cg_tutorial_appendix_e.html
			#include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            // fixed4 _Color;

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            UNITY_DECLARE_TEX2DARRAY(_MainTexPaired);
            UNITY_DECLARE_TEX2DARRAY(_NormalMap);
            UNITY_DECLARE_TEX2DARRAY(_NormalMapPaired);
            UNITY_DECLARE_TEX2DARRAY(_MainTexDoor);
            UNITY_DECLARE_TEX2DARRAY(_NormalMapDoor);

            sampler2D _StairTex;
            // sampler2D _DensityTex;

            Texture2D _HeightMap;
            SamplerState sampler_HeightMap;

            float _HeightMultiplier;

            float _TileSize;
            float _InverseTileSize;

            float _UseFlatShading;

            struct VertexData {  // in local space
                float4 vertex : POSITION;
                float4 texcoord : TEXCOORD0;  // uv, then WindowTexture- (± for paired or not), then DoorTexture-depth/index
                float3 normal : NORMAL;
                float3 tangent : TANGENT;
                float4 color : COLOR0; // 0,0,0 -> no color, use random color, alpha encodes isGroundLevel
                // float4 color2 : COLOR1; // TODO: or maybee use alpha-ch bits -> for doot offset
            };

            struct FragmentData {
                float4 pos : SV_POSITION;  // in world space
	            float4 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 tangent : TEXCOORD2;
                float4 worldPos : TEXCOORD3;
                float3 facadeCol : TEXCOORD4;
                float3 frameCol : TEXCOORD5;
                float3 windowCol : TEXCOORD6;
                float isGroundLevel : TEXCOORD7;
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

            // from https://www.shadertoy.com/view/lsS3Wc
            float3 hsl2rgb(float3 c)
            {
                float3 rgb = clamp( abs(fmod(c.x*6.0+float3(0.0,4.0,2.0),6.0)-3.0)-1.0, 0.0, 1.0 );
                return c.z * lerp(float3(1,1,1), rgb, c.y);
            }

            // from https://de.wikipedia.org/wiki/YUV-Farbmodell
            float3 yuv2rgb(float3 c)
            {
                float r = c.r + 1.14 * c.b;
                float b = c.r + 2.028 * c.g;
                return float3(r, 1.704 * c.r - 0.509 * r - 0.194 * b, b);
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
                    rand3dTo1d(value, float3(12.989, 78.233, 37.719)),
                    rand3dTo1d(value, float3(39.346, 11.135, 83.155)),
                    rand3dTo1d(value, float3(73.156, 52.235, 09.151))
                );
            }

            // random uv-scale in to get more variation (less facede betweeen windows) out of the same texture
            // replaces the superlarge
            float2 uvScale(float2 uv, float rand){
                float zoom = lerp(0.0, 0.14, rand*rand);
                // uvZoomed = value between zoom and 1-zoom
                float2 uvZoomed = uv - frac(uv) + ((1 - 2 * zoom) * frac(uv) + float2(zoom, zoom));
                // lerp to get continous uv-coordiantes (from 0 to 1 and not from zoom to 1-zoom)
                return lerp(uvZoomed, uv, 256*pow(frac(uv)-0.5, 8));
            }

            float3 rgb2grey(float3 c)
            {
                // only generates lighter grays
                float gray = (c.r * 0.299 + c.g * 0.587 + c.b * 0.114) * 0.5 + 0.5;
                return float3(gray, gray, gray);
            }

            float rand2alpha(float rand, float center){
                return min(max(pow(rand-center, 3)*100 + 0.5, 0), 1);
            }

            float2 rotate(float2 pos, float angle){
                return float2(
                    cos(angle) * pos.x - sin(angle) * pos.y,
                    sin(angle) * pos.x + cos(angle) * pos.y
                );
            }

            FragmentData vertexPgrm(VertexData input_data) {
                FragmentData fData;

                // calculate random colors
                float rand1 = rand3dTo1d(mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz);
                float rand2 = rand3dTo1d(float3(rand1, rand1, 0));
                float rand3 = rand3dTo1d(float3(0, rand1, rand1));
                // generate random brown shades in yuv-color-scale
                float3 yuv = float3(
                    lerp(
                        (1-(1/(rand1+1))) * (1-(1/(rand1+1))),
                        (1/(rand1+1))*(1/(rand1+1)),
                        rand2
                    ),
                    lerp(-0.08, -0.03, rand2),
                    lerp(0.04, 0.08, rand3)
                );
                float3 yuv2 = float3(
                    rand3,
                    lerp(-0.08, -0.03, rand1),
                    // take the one with greatest distance to lerp(0.04, 0.08, rand3)
                    abs(lerp(0.04, 0.08, rand1) - lerp(0.04, 0.08, rand3)) > abs(lerp(0.04, 0.08, rand2) - lerp(0.04, 0.08, rand3)) ? lerp(0.04, 0.08, rand1) : lerp(0.04, 0.08, rand2)
                );

                fData.facadeCol = yuv2rgb(yuv);
                fData.facadeCol = lerp(fData.facadeCol, rgb2grey(fData.facadeCol), rand2alpha(rand3, 0.5));
                fData.facadeCol = lerp(fData.facadeCol, input_data.color.rgb, min(dot(input_data.color.rgb, float3(1000, 1000, 1000)), 1));

                fData.frameCol  = yuv2rgb(yuv2);
                fData.windowCol = hsl2rgb(float3(lerp(0.55, 0.64, rand1), lerp(0.6, 1, rand2), lerp(0.7, 0.95, rand3)));

                // calculate vertex pos (height offset)
                float height = heightFromRGB(_HeightMap.SampleLevel(sampler_HeightMap, uvFromWorldPos(mul(unity_ObjectToWorld, float4(0,0,0,1))), 0).rgb);
                fData.pos = input_data.vertex;
                fData.pos.y += height * _HeightMultiplier;

                fData.uv = input_data.texcoord;
                fData.isGroundLevel = input_data.color.a;
                fData.normal = UnityObjectToWorldNormal(input_data.normal);
                fData.tangent = UnityObjectToWorldNormal(input_data.tangent);
                // Calculate world position for face-normal calculation in fragment shader (flat shading).
				fData.worldPos = mul(unity_ObjectToWorld, input_data.vertex);
                // fData.view_dir = normalize(WorldSpaceViewDir(fData.pos));

                fData.pos = UnityObjectToClipPos(fData.pos);

                return fData;
            }


			float4 fragmentPgrm (FragmentData data) : SV_Target
			{
                float frag_rand = rand3dTo1d(mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz);

                // renormalize (since the interpolation between vertices doesn't produce unit-vectors)
                data.normal = normalize(data.normal);
                data.tangent = normalize(data.tangent);
                float3 view_dir = normalize(_WorldSpaceCameraPos - data.pos);

                // FLAT SHADING
                float3 flatNormal = normalize(cross(ddy(data.worldPos), ddx(data.worldPos)));
                data.normal = lerp(data.normal, flatNormal, _UseFlatShading);  // nof if, lerp insted of if

                // random uv-zoom (to get more variations; varriing spacing between windows)
                // data.uv = float4(uvScale(data.uv.xy, frag_rand), data.uv.z, data.uv.a);

                float door_offset = floor(frag_rand * 5);
                float door_repetition = 10 + floor(frag_rand * 10);
                float door_blend = 1 - min(floor(min(fmod(floor(data.uv.x) + door_offset, door_repetition), 1) + max(floor(data.uv.y), 0)) + (1 - data.isGroundLevel), 1);
                // no door where the height difference between door and floor is >1
                float heightCenter = heightFromRGB(_HeightMap.SampleLevel(sampler_HeightMap, uvFromWorldPos(mul(unity_ObjectToWorld, float4(0,0,0,1))), 0).rgb) * _HeightMultiplier;
                float heightDoor = heightFromRGB(_HeightMap.SampleLevel(sampler_HeightMap, uvFromWorldPos(data.worldPos), 0).rgb) * _HeightMultiplier;
                door_blend = saturate(door_blend - (heightCenter - heightDoor > 1 ? 1 : 0));

                float isBelowGround = 1 - saturate(sign(data.uv.y) + 1);

                float4 tex_col = lerp(  // paired or not paired?
                    UNITY_SAMPLE_TEX2DARRAY(_MainTexPaired,  float3(data.uv.x, 1.0-data.uv.y, abs(data.uv.z))),
                    UNITY_SAMPLE_TEX2DARRAY(_MainTex,  float3(data.uv.x, 1.0-data.uv.y, abs(data.uv.z))),
                    max(sign(data.uv.z), 0)
                );
                // is curch window -> no door?
                door_blend = saturate(door_blend - (1 - tex_col.a));
                tex_col = lerp(  // door?
                    tex_col,
                    UNITY_SAMPLE_TEX2DARRAY(_MainTexDoor,  float3(data.uv.x, 1.0-data.uv.y, data.uv.a)),
                    door_blend
                );
                tex_col = lerp(  // below ground?
                    tex_col,
                    tex2D(_StairTex,  float2(data.uv.x, max(data.uv.y * 10, -0.95))),
                    isBelowGround
                );
                tex_col = lerp(  // below ground + no door?
                    tex_col,
                    UNITY_SAMPLE_TEX2DARRAY(_MainTex,  float3(0,0, abs(data.uv.z))),
                    isBelowGround - door_blend
                );

                // use normal-map
                float3 normalFromMap = lerp(  // paired or not paired?
                    UNITY_SAMPLE_TEX2DARRAY(_NormalMapPaired,  float3(data.uv.x, 1.0-data.uv.y, abs(data.uv.z))).rgb,
                    UNITY_SAMPLE_TEX2DARRAY(_NormalMap,  float3(data.uv.x, 1.0-data.uv.y, abs(data.uv.z))).rgb,
                    max(sign(data.uv.z), 0)
                );
                normalFromMap = lerp(  // door?
                    normalFromMap,
                    UNITY_SAMPLE_TEX2DARRAY(_NormalMapDoor,  float3(data.uv.x, 1.0-data.uv.y, data.uv.a)).rgb,
                    door_blend
                );
                normalFromMap = lerp(  // below ground?
                    normalFromMap,
                    UNITY_SAMPLE_TEX2DARRAY(_NormalMapDoor,  float3(0,0, data.uv.a)).rgb,  // front-normal
                    isBelowGround
                );
                normalFromMap = float3(1.0 - normalFromMap.r, 1.0 - normalFromMap.g, normalFromMap.b) * 2.0 - 1.0;
                float3 bi_tangent = cross(data.tangent, data.normal);
                // real_normal is the normal in world-space
                float3 real_normal = normalize(normalFromMap.r * data.tangent + normalFromMap.g * bi_tangent + normalFromMap.b * data.normal);


                // float4 density = tex2D(_DensityTex, uvFromWorldPos(data.worldPos));
                // // // data.worldPos.y is the height of the mesh

                // tex_col = lerp(
                //     tex_col,
                //     float4(1,0,0,tex_col.a),
                //     density.b > 0.5 ? 1:0
                // );

                // r = facade, g = glass, b = beam/wood
                float3 surface_col = tex_col.r * data.facadeCol + tex_col.g * data.windowCol + tex_col.b * data.frameCol;


                // ambient lighting (general light from anywhere), so it isn't dark without a light
                float ambient = 0.3;

                // diffuse lighting
                float diffuse = saturate(dot(real_normal, _WorldSpaceLightPos0.xyz));

                // specular lighting
                float specular_strength = 0.5;
                int specular_spot_intensity = 32;
                float3 reflected_light_dir = normalize(reflect(-_WorldSpaceLightPos0.xyz, real_normal));
                float specular = pow(max(dot(view_dir, reflected_light_dir), 0.0), specular_spot_intensity) * specular_strength * (tex_col.g * 0.75f + 0.25f);


                // mosaic
                // from https://www.ronja-tutorials.com/post/028-voronoi-noise/
                // wps = world position scaled
                float3 wps = data.worldPos * float3(4, 4, 4);
                float minDist = 1.;
                float3 closestCell;  // vec to closest cell
                float3 toClosestCell;  // the closest cell (cell coords)
                for (int x= -1; x <= 1; x++) {
                    for (int y= -1; y <= 1; y++) {
                        for (int z= -1; z <= 1; z++) {
                            float3 cell = floor(wps) + float3(x,y,z);
                            float3 cellPosition = cell + rand3dTo3d(cell);
                            float3 toCell = cellPosition - wps;
                            float distToCell = length(toCell);

                            if(distToCell < minDist){
                                minDist = distToCell;
                                closestCell = cell;
                                toClosestCell = toCell;
                            }
                        }
                    }
                }
                float minEdgeDist = 1.;
                for (int x= -1; x <= 1; x++) {
                    for (int y= -1; y <= 1; y++) {
                        for (int z= -1; z <= 1; z++) {
                            float3 cell = floor(wps) + float3(x,y,z);
                            float3 cellPosition = cell + rand3dTo3d(cell);
                            float3 toCell = cellPosition - wps;

                            float3 diffToClosestCell = abs(closestCell - cell);
                            bool isClosestCell = diffToClosestCell.x + diffToClosestCell.y + diffToClosestCell.z < 0.1;
                            if(!isClosestCell){
                                float3 toCenter = (toClosestCell + toCell) * 0.5;
                                float3 cellDifference = normalize(toCell - toClosestCell);
                                float edgeDistance = dot(toCenter, cellDifference);
                                minEdgeDist = min(minEdgeDist, edgeDistance);
                            }
                        }
                    }
                }
                float valueChange = length(fwidth(wps)) * 0.5;
                float isBorder = 1 - smoothstep(0.05 - valueChange, 0.05 + valueChange, minEdgeDist);
                surface_col = lerp(surface_col, lerp(rand3dTo3d(closestCell), float3(0,0,0), isBorder), tex_col.g * (1 - tex_col.a));


                // add all together
                float3 col = surface_col * _LightColor0.xyz * min(ambient + diffuse + specular, 1);

                // circular defects
                wps = data.worldPos * float3(200, 200, 200);
                wps = wps + float3(rand3dTo1d(floor(wps))*5.459, rand3dTo1d(floor(wps))*2.8749, rand3dTo1d(floor(wps))*4.541);  // random offset
                float3 diff = floor(wps) + float3(0.5, 0.5, 0.5) - wps;  // difference to center
                float dist = tex_col.r > 0.5f ? min(length(diff) * 1, 1) : 1;

                // bricks
                wps = data.worldPos * float3(5*frag_rand, 12*frag_rand, 5*frag_rand);
                // use normal to align bricks with x-axis
                float rot = 1.5707963268f - atan2(data.normal.z, data.normal.x);  // pi/2 - atan2(normal.xz)
                float2 wpsXZ = rotate(float2(wps.x, wps.z), (int)(rot * 10) * 0.1f);
                wps = float3(wpsXZ.x, wps.y, wpsXZ.y);
                wps += float3(floor(wps.y) * 0.5, 0, floor(wps.y) * 0.5);
                diff = wps - floor(wps);  // difference to center
                dist = tex_col.r > 0.5f && surface_col.r > max(surface_col.g, surface_col.b) && frag_rand > 0.5 ?
                        1 - max(min(0.03/abs(diff.x-0.5)-0.05, 1), min(20*pow(diff.y-0.5, 4), 1)) : dist;

                return float4(
                    lerp(col.r, col.r - 0.05, 1 - dist),
                    lerp(col.g, col.g - 0.05, 1 - dist),
                    lerp(col.b, col.b - 0.05, 1 - dist),
                    1
                );
			}
			ENDCG
		}


        // Pass
        // {
        //     Tags {"LightMode"="ShadowCaster"}

        //     ColorMask 0

        //     CGPROGRAM
        //     #pragma target 4.6  // min version for tessellation
        //     #pragma vertex vertexPgrm
		// 	#pragma fragment fragmentPgrm
        //     #pragma multi_compile_shadowcaster
        //     #include "UnityCG.cginc"

        //     struct VertexData {  // in local space
        //         float4 vertex : POSITION;
        //         float3 normal : NORMAL;
        //     };

        //     struct FragmentData {
        //         float4 pos : SV_POSITION;  // in world space
        //         // float3 bary : TEXCOORD4;
        //     };

        //     FragmentData vertexPgrm(VertexData input_data) {
        //         FragmentData fData;

        //         float height = heightFromRGB(_HeightMap.SampleLevel(sampler_HeightMap, uvFromWorldPos(mul(unity_ObjectToWorld, float4(0,0,0,1))), 0).rgb);
        //         fData.pos = input_data.vertex;
        //         fData.pos.y += height * _HeightMultiplier;
        //         // use bias to prevent shadow acne (kind of z-fighting)
        //         fData.pos = UnityClipSpaceShadowCasterPos(fData.pos.xyz, input_data.normal);
        //         return fData;
        //     }

        //     float4 fragmentPgrm (FragmentData data) : SV_Target
		// 	{
        //         // return value is not saved anywhere, this pass is only used for the depth-texture
        //         return float4(0,0,0,0);
		// 	}
        //     ENDCG
        // }

        // Pass
        // {
        //     Tags {"LightMode"="ShadowCaster"}

        //     CGPROGRAM
        //     #pragma target 4.6  // min version for tessellation
        //     #pragma vertex vertexPgrm
		// 	#pragma fragment fragmentPgrm
        //     #pragma multi_compile _ SHADOWS_SCREEN
        //     #include "UnityCG.cginc"

        //     struct VertexData {  // in local space
        //         float4 vertex : POSITION;
        //         float3 normal : NORMAL;
        //     };

        //     struct FragmentData {
        //         float4 pos : SV_POSITION;  // in world space
        //         // float3 bary : TEXCOORD4;
        //     };

        //     FragmentData vertexPgrm(VertexData input_data) {
        //         FragmentData fData;

        //         float height = heightFromRGB(_HeightMap.SampleLevel(sampler_HeightMap, uvFromWorldPos(mul(unity_ObjectToWorld, float4(0,0,0,1))), 0).rgb);
        //         fData.pos = input_data.vertex;
        //         fData.pos.y += height * _HeightMultiplier;

        //         fData.pos = UnityClipSpaceShadowCasterPos(fData.pos.xyz, input_data.normal);
        //         fData.pos = UnityApplyLinearShadowBias(fData.pos);
        //         return fData;
        //     }

        //     float4 fragmentPgrm (FragmentData data) : SV_Target
		// 	{
        //         // return value is not saved anywhere, this pass is only used for the depth-texture
        //         return float4(0,0,0,0);
		// 	}
        //     ENDCG
        // }
	}
}
