Shader "Custom/MosaicShader"
{
    Properties
    {
        // _Color ("Color", Color) = (1,1,1,1)
        _BorderColor ("Border Color", Color) = (0,0,0,1)
        _Scale ("Scale", float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing // gpu instancing
            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // gpu instancing
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // gpu instancing
            };

            float _Scale;
            fixed4 _BorderColor;

            // from https://www.ronja-tutorials.com/post/024-white-noise/
            float rand3dTo1d(float3 value, float3 dotDir = float3(12.9898, 78.233, 37.719)){
                float3 smallValue = sin(value);
                float random = dot(smallValue, dotDir);
                random = frac(sin(random) * 143758.5453);
                return random;
            }
            float2 rand3dTo2d(float3 value){
                return float2(
                    rand3dTo1d(value, float3(12.989, 78.233, 37.719)),
                    rand3dTo1d(value, float3(39.346, 11.135, 83.155))
                );
            }
            float3 rand3dTo3d(float3 value){
                return float3(
                    rand3dTo1d(value, float3(12.989, 78.233, 37.719)),
                    rand3dTo1d(value, float3(39.346, 11.135, 83.155)),
                    rand3dTo1d(value, float3(73.156, 52.235, 09.151))
                );
            }

            // gpu instancing
            // UNITY_INSTANCING_BUFFER_START(Props)
            // UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            // UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (Input input)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(input); // gpu instancing
                UNITY_TRANSFER_INSTANCE_ID(input, o); // gpu instancing
                o.vertex = UnityObjectToClipPos(input.vertex);
                o.uv = input.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); // gpu instancing
                fixed4 col = fixed4(0,0,0,1);

                // Scale
                float2 st = i.uv * _Scale;

                // from https://www.ronja-tutorials.com/post/028-voronoi-noise/
                float2 baseCell = floor(st);

                float minDist = 1.;
                float2 toClosestCell;  // the closest cell (cell coords)
                float2 closestCell;  // vec to closest cell

                for (int y= -1; y <= 1; y++) {
                    for (int x= -1; x <= 1; x++) {
                        float2 cell = baseCell + float2(x,y);
                        float2 cellPosition = cell + rand3dTo2d(float3(cell.x, cell.y, unity_InstanceID));
                        float2 toCell = cellPosition - st;
                        float distToCell = length(toCell);

                        if(distToCell < minDist){
                            minDist = distToCell;
                            closestCell = cell;
                            toClosestCell = toCell;
                        }
                    }
                }

                //second pass to find the distance to the closest edge
                float minEdgeDist = 1.;
                for (int y= -1; y <= 1; y++) {
                    for (int x= -1; x <= 1; x++) {
                        float2 cell = baseCell + float2(x,y);
                        float2 cellPosition = cell + rand3dTo2d(float3(cell.x, cell.y, unity_InstanceID));
                        float2 toCell = cellPosition - st;

                        float2 diffToClosestCell = abs(closestCell - cell);
                        bool isClosestCell = diffToClosestCell.x + diffToClosestCell.y < 0.1;
                        if(!isClosestCell){
                            float2 toCenter = (toClosestCell + toCell) * 0.5;
                            float2 cellDifference = normalize(toCell - toClosestCell);
                            float edgeDistance = dot(toCenter, cellDifference);
                            minEdgeDist = min(minEdgeDist, edgeDistance);
                        }
                    }
                }

                float3 cellColor = rand3dTo3d(float3(closestCell.x, closestCell.y, unity_InstanceID));
                float valueChange = length(fwidth(st)) * 0.5;
                float isBorder = 1 - smoothstep(0.05 - valueChange, 0.05 + valueChange, minEdgeDist);
                col = fixed4(lerp(cellColor, _BorderColor, isBorder), 1);

                return col; // UNITY_ACCESS_INSTANCED_PROP(Props, _Color); // gpu instancing
            }
            ENDCG
        }
    }
}
