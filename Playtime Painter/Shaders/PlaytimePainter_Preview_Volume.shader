﻿Shader "Playtime Painter/Editor/Preview/Volume" {
	Properties{
		_PreviewTex("Base (RGB)", 2D) = "white" { }
		VOLUME_H_SLICES("Baked Shadow Slices", Vector) = (0,0,0,0)
		VOLUME_POSITION_N_SIZE("Volume Position & Size", Vector) = (0,0,0,0)
	}

	Category{

		Tags{
			"Queue" = "Geometry"
			"RenderType" = "Opaque"
		}

		ColorMask RGBA
		Cull off

		SubShader{
			Pass{

				CGPROGRAM

				#pragma multi_compile  PREVIEW_RGB PREVIEW_ALPHA
				#pragma multi_compile  BLIT_MODE_ALPHABLEND BLIT_MODE_ADD BLIT_MODE_SUBTRACT BLIT_MODE_COPY

				#pragma vertex vert
				#pragma fragment frag

				#include "PlaytimePainter_cg.cginc"
				#include "Assets/Tools/Playtime Painter/Shaders/quizcanners_cg.cginc"

				sampler2D _PreviewTex;
				float4 _PreviewTex_ST;
				float4 _PreviewTex_TexelSize;
				float4 VOLUME_H_SLICES;
				float4 VOLUME_POSITION_N_SIZE;

				struct v2f {
					float4 pos : POSITION;
					float2 texcoord : TEXCOORD0;
					float3 worldPos : TEXCOORD1;
				};

				v2f vert(appdata_full v) {
					v2f o;
					o.pos = UnityObjectToClipPos(v.vertex);

					o.texcoord = TRANSFORM_TEX(v.texcoord.xy, _PreviewTex);
					
					o.worldPos = mul(unity_ObjectToWorld, v.vertex);

					return o;
				}

				float4 frag(v2f i) : COLOR{
							float dist = length(i.worldPos.xyz - _WorldSpaceCameraPos.xyz);

				float srcAlpha = 1;

					#if BLIT_MODE_COPY
					_brushColor = SampleVolume(_SourceTexture, i.worldPos, VOLUME_POSITION_N_SIZE, VOLUME_H_SLICES);
					srcAlpha = _brushColor.a;
					#endif

					float4 col = 0;
					float alpha = 1;

					alpha *= checkersFromWorldPosition(i.worldPos.xyz,dist);

					col = SampleVolume(_PreviewTex, i.worldPos, VOLUME_POSITION_N_SIZE, VOLUME_H_SLICES);

					alpha *= saturate(positionToAlpha(i.worldPos));

					float differentColor = min(0.5, (abs(col.g - _brushColor.g) + abs(col.r - _brushColor.r) + abs(col.b - _brushColor.b)) * 8);

					_brushColor = _brushColor * (differentColor + 0.5);

					#if PREVIEW_SAMPLING_DISPLACEMENT

					float resX = (i.texcoord.x + (col.r - 0.5) * 2);
					float resY = (i.texcoord.y + (col.g - 0.5) * 2);

					float edge = abs(0.5 - ((resX*_brushSamplingDisplacement.z) % 1)) + abs(0.5 - (resY*_brushSamplingDisplacement.w) % 1);

					float distX = (resX - _brushSamplingDisplacement.x);
					float distY = (resY - _brushSamplingDisplacement.y);
					col.rgb = saturate(1 - sqrt(distX*distX + distY * distY) * 8) + saturate(edge);

					#endif

					#if PREVIEW_ALPHA
					col = col * _brushMask + 0.5*(1 - _brushMask) + col.a*_brushMask.a; //  col.a;
					#endif

					#if BLIT_MODE_ALPHABLEND || BLIT_MODE_COPY
						col = AlphaBlitOpaquePreview(alpha, _brushColor,  i.texcoord.xy , col, srcAlpha);
					#endif

					#if BLIT_MODE_ADD
						col = addWithDestBufferPreview(alpha*0.4, _brushColor,  i.texcoord.xy, col, srcAlpha);
					#endif

					#if BLIT_MODE_SUBTRACT
						col = subtractFromDestBufferPreview(alpha*0.4, _brushColor,  i.texcoord.xy, col);
					#endif

					return col;

				}
				ENDCG
			}
			UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
		}
	}
}