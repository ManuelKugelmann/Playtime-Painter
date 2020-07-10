﻿Shader "Playtime Painter/UI/Rounded/Box"
{
	Properties{
		[PerRendererData]_MainTex("Albedo (RGB)", 2D) = "black" {}
		_Edges("Sharpness", Range(0,1)) = 0.5
		[Toggle(TRIMMED)] trimmed("Trimmed Corners", Float) = 0
		[Toggle(_SOFT_FADE)] softfade("Soft Fade", Float) = 0
	}

	Category{

		Tags{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"PreviewType" = "Plane"
			"RenderType" = "Transparent"
			"PixelPerfectUI" = "Simple"
			"SpriteRole" = "Hide"
			"PerEdgeData" = "Linked"
		}

		ColorMask RGB
		Cull Off
		ZWrite Off
		ZTest Off
		Blend SrcAlpha OneMinusSrcAlpha

		SubShader{
			Pass{

				CGPROGRAM

				#include "UnityCG.cginc"

				#pragma vertex vert
				#pragma fragment frag

				#pragma multi_compile_instancing
				#pragma shader_feature __ TRIMMED
				#pragma shader_feature __ _SOFT_FADE

				struct v2f {
					float4 pos : SV_POSITION;
					float4 texcoord : TEXCOORD0;
					float4 projPos : TEXCOORD1;
					float4 precompute : TEXCOORD2;
					float2 offUV : TEXCOORD3;
					float4 color: COLOR;
				};

				float _Edges;

				v2f vert(appdata_full v) {
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v);
					o.pos =				UnityObjectToClipPos(v.vertex);
					o.texcoord.xy =		v.texcoord.xy;
					o.color =			v.color;

#if _SOFT_FADE
					_Edges *= o.color.a * o.color.a;
#endif
					o.texcoord.zw =		v.texcoord1.xy;
					o.texcoord.z =		4 - _Edges * 3;
					o.projPos.xy =		v.normal.xy;
					o.projPos.zw =		max(0, float2(v.texcoord1.x, -v.texcoord1.x));

					o.precompute.w =	1 / (1.0001 - o.texcoord.w);
					o.precompute.xy =	1 / (1.0001 - o.projPos.zw);
					o.precompute.z =	1 + _Edges * 32;

					o.offUV.xy =		(o.texcoord.xy - 0.5)*2;
			
					return o;
				}

				float4 frag(v2f o) : COLOR{

					float4 _ProjTexPos =	o.projPos;
					float _Courners =		o.texcoord.w;
					float deCourners = 1 - _Courners;
					float something =		o.precompute.w;
					float2 uv =				abs(o.offUV);

					uv = max(0, uv - _ProjTexPos.zw) * o.precompute.xy;

					uv = max(0, uv - _Courners) * something;

					#if TRIMMED

						float dist = (uv.x + uv.y); 

					#else
						float dist = dot(uv, uv);
					#endif

					float alpha = saturate(1 - dist);

					alpha = min(1, pow(alpha * o.precompute.z, o.texcoord.z));

					float dx = abs(ddx(alpha));
					float dy = abs(ddy(alpha));
					float mip = (dx + dy) * 200;

		

					o.color.a *= alpha;

					return o.color;
				}
				ENDCG
			}
		}
		Fallback "Legacy Shaders/Transparent/VertexLit"
	}
}
