#include <core/materials/shaders/api/common.h>

#ifndef NOESIS_VERTEX_FORMAT
	#error "NOESIS_VERTEX_FORMAT must be defined (0..15)"
#endif

// Vertex input per Noesis::Shader::Vertex::Format.
// Unigine assigns vertex attribute slot N → SPIR-V/DXIL location N.
// POSITION = slot 0, TEXCOORD(N-1) = slot N.

#if NOESIS_VERTEX_FORMAT == 0   // Pos
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
END
#elif NOESIS_VERTEX_FORMAT == 1 // PosColor
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float4, 1, TEXCOORD0)
END
#elif NOESIS_VERTEX_FORMAT == 2 // PosTex0
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float2, 1, TEXCOORD0)
END
#elif NOESIS_VERTEX_FORMAT == 3 // PosTex0Rect
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float2, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float4, 2, TEXCOORD1)
END
#elif NOESIS_VERTEX_FORMAT == 4 // PosTex0RectTile
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float2, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float4, 2, TEXCOORD1)
	INIT_ATTRIBUTE(float4, 3, TEXCOORD2)
END
#elif NOESIS_VERTEX_FORMAT == 5 // PosColorCoverage
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float4, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float,  2, TEXCOORD1)
END
#elif NOESIS_VERTEX_FORMAT == 6 // PosTex0Coverage
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float2, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float,  2, TEXCOORD1)
END
#elif NOESIS_VERTEX_FORMAT == 7 // PosTex0CoverageRect
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float2, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float,  2, TEXCOORD1)
	INIT_ATTRIBUTE(float4, 3, TEXCOORD2)
END
#elif NOESIS_VERTEX_FORMAT == 8 // PosTex0CoverageRectTile
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float2, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float,  2, TEXCOORD1)
	INIT_ATTRIBUTE(float4, 3, TEXCOORD2)
	INIT_ATTRIBUTE(float4, 4, TEXCOORD3)
END
#elif NOESIS_VERTEX_FORMAT == 9 // PosColorTex1
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float4, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float2, 2, TEXCOORD1)
END
#elif NOESIS_VERTEX_FORMAT == 10 // PosTex0Tex1
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float2, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float2, 2, TEXCOORD1)
END
#elif NOESIS_VERTEX_FORMAT == 11 // PosTex0Tex1Rect
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float2, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float2, 2, TEXCOORD1)
	INIT_ATTRIBUTE(float4, 3, TEXCOORD2)
END
#elif NOESIS_VERTEX_FORMAT == 12 // PosTex0Tex1RectTile
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float2, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float2, 2, TEXCOORD1)
	INIT_ATTRIBUTE(float4, 3, TEXCOORD2)
	INIT_ATTRIBUTE(float4, 4, TEXCOORD3)
END
#elif NOESIS_VERTEX_FORMAT == 13 // PosColorTex0Tex1
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float4, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float2, 2, TEXCOORD1)
	INIT_ATTRIBUTE(float2, 3, TEXCOORD2)
END
#elif NOESIS_VERTEX_FORMAT == 14 // PosColorTex1Rect
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float4, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float2, 2, TEXCOORD1)
	INIT_ATTRIBUTE(float4, 3, TEXCOORD2)
END
#elif NOESIS_VERTEX_FORMAT == 15 // PosColorTex0RectImagePos
STRUCT(VERTEX_IN)
	INIT_ATTRIBUTE(float2, 0, POSITION)
	INIT_ATTRIBUTE(float4, 1, TEXCOORD0)
	INIT_ATTRIBUTE(float2, 2, TEXCOORD1)
	INIT_ATTRIBUTE(float4, 3, TEXCOORD2)
	INIT_ATTRIBUTE(float4, 4, TEXCOORD3)
END
#endif

// Interpolants VS→PS. Slots match ShaderPS.hlsl struct In semantics (TEXCOORD0..N → data_N).
STRUCT(VERTEX_OUT)
	INIT_POSITION
#ifdef HAS_COLOR
	MODIFIER_NOINTERPOLATION INIT_OUT(half4, 0)
#endif
#ifdef HAS_UV0
	INIT_OUT(float2, 1)
#endif
#ifdef HAS_UV1
	INIT_OUT(float2, 2)
#endif
#ifdef HAS_UV2
	INIT_OUT(float2, 3)
#endif
#ifdef HAS_UV3
	INIT_OUT(float2, 4)
#endif
#ifdef HAS_ST1
	INIT_OUT(float4, 5)
#endif
#ifdef HAS_COVERAGE
	INIT_OUT(half, 6)
#endif
#ifdef HAS_RECT
	MODIFIER_NOINTERPOLATION INIT_OUT(float4, 7)
#endif
#ifdef HAS_TILE
	MODIFIER_NOINTERPOLATION INIT_OUT(float4, 8)
#endif
#ifdef HAS_IMAGE_POSITION
	INIT_OUT(float4, 9)
#endif
END

CBUFFER(Buffer0)
#ifdef STEREO_RENDERING
	UNIFORM float4x4 projectionMtx[2];
#else
	UNIFORM float4x4 projectionMtx;
#endif
END

#ifdef HAS_ST1
CBUFFER(Buffer1)
	UNIFORM float2 textureSize;
END
#endif

#pragma warning(disable: 3571)

float SRGBToLinear(float value)
{
	if (value <= 0.04045)
		return value * (1.0 / 12.92);
	return pow(value * (1.0 / 1.055) + 0.0521327, 2.4);
}

// Input attribute slot for uv0: slot 1 when no color, slot 2 when color precedes.
// Input attribute slot for uv1: slot 2 when no color/uv0 precede or one of them, slot 3 for PosColorTex0Tex1 (fmt 13).
// Coverage: always slot 2 when present.
// Rect/tile/imagePos: slot varies (dispatched by format below).

MAIN_BEGIN(VERTEX_OUT, VERTEX_IN)

#ifdef STEREO_RENDERING
	OUT_POSITION = mul(float4(IN_ATTRIBUTE(0).xy, 0, 1), projectionMtx[IN_ATTRIBUTE(0).z]);
	OUT_RT_INDEX = (uint)IN_ATTRIBUTE(0).z;
#else
	// Depth convention is handled by the render device: it reports DeviceCaps.DepthRangeZeroToOne
	// so Noesis emits a D3D-style projection (clip z in [0, w]) matching Unigine's D3D12 clip volume.
	OUT_POSITION = mul(float4(IN_ATTRIBUTE(0).xy, 0, 1), projectionMtx);
#endif

#ifdef HAS_COLOR
	// Color is always at slot 1 across all color-containing formats.
  #ifdef LINEAR_COLOR_SPACE
	OUT_DATA(0) = half4(
		(half)SRGBToLinear(IN_ATTRIBUTE(1).r),
		(half)SRGBToLinear(IN_ATTRIBUTE(1).g),
		(half)SRGBToLinear(IN_ATTRIBUTE(1).b),
		(half)IN_ATTRIBUTE(1).a);
  #else
	OUT_DATA(0) = (half4)IN_ATTRIBUTE(1);
  #endif
#endif

// Downsample: HAS_UV2 marks 2×2 gather from a single (uv0, uv1) input.
#ifdef HAS_UV2
  // Format 10 (PosTex0Tex1): slot1=uv0, slot2=uv1 (no color).
  #define NOESIS_UV0_SLOT 1
  #define NOESIS_UV1_SLOT 2
	OUT_DATA(1) = IN_ATTRIBUTE(NOESIS_UV0_SLOT).xy + float2( IN_ATTRIBUTE(NOESIS_UV1_SLOT).x,  IN_ATTRIBUTE(NOESIS_UV1_SLOT).y);
	OUT_DATA(2) = IN_ATTRIBUTE(NOESIS_UV0_SLOT).xy + float2( IN_ATTRIBUTE(NOESIS_UV1_SLOT).x, -IN_ATTRIBUTE(NOESIS_UV1_SLOT).y);
	OUT_DATA(3) = IN_ATTRIBUTE(NOESIS_UV0_SLOT).xy + float2(-IN_ATTRIBUTE(NOESIS_UV1_SLOT).x,  IN_ATTRIBUTE(NOESIS_UV1_SLOT).y);
	OUT_DATA(4) = IN_ATTRIBUTE(NOESIS_UV0_SLOT).xy + float2(-IN_ATTRIBUTE(NOESIS_UV1_SLOT).x, -IN_ATTRIBUTE(NOESIS_UV1_SLOT).y);
  #undef NOESIS_UV0_SLOT
  #undef NOESIS_UV1_SLOT
#else

#ifdef HAS_UV0
  // uv0 is at slot 1 for non-color formats, slot 2 for color-containing formats.
  #if NOESIS_VERTEX_FORMAT == 13 || NOESIS_VERTEX_FORMAT == 15
	OUT_DATA(1) = IN_ATTRIBUTE(2).xy;
  #else
	OUT_DATA(1) = IN_ATTRIBUTE(1).xy;
  #endif
#endif

#ifdef HAS_UV1
  // uv1 slot: 2 for fmts 9,10,11,12,14; 3 for fmt 13 (PosColorTex0Tex1).
  #if NOESIS_VERTEX_FORMAT == 13
	OUT_DATA(2) = IN_ATTRIBUTE(3).xy;
  #else
	OUT_DATA(2) = IN_ATTRIBUTE(2).xy;
  #endif
#endif

#endif // HAS_UV2 else

#ifdef HAS_ST1
  // textureSize cbuffer + uv1 to compute screenspace texel offsets.
  // uv1 slot same rules as HAS_UV1.
  #if NOESIS_VERTEX_FORMAT == 13
	OUT_DATA(5) = float4(IN_ATTRIBUTE(3).xy * textureSize.xy, 1.0 / (3.0 * textureSize.xy));
  #else
	OUT_DATA(5) = float4(IN_ATTRIBUTE(2).xy * textureSize.xy, 1.0 / (3.0 * textureSize.xy));
  #endif
#endif

#ifdef HAS_COVERAGE
	// Coverage is always slot 2 (after pos + one other attr: color or uv0).
	OUT_DATA(6) = (half)IN_ATTRIBUTE(2).x;
#endif

#ifdef HAS_RECT
  // Rect slot depends on format (dispatched below).
  #if NOESIS_VERTEX_FORMAT == 3 || NOESIS_VERTEX_FORMAT == 4
	// PosTex0Rect, PosTex0RectTile: pos(0) uv0(1) rect(2)
	OUT_DATA(7) = IN_ATTRIBUTE(2);
  #elif NOESIS_VERTEX_FORMAT == 7 || NOESIS_VERTEX_FORMAT == 8
	// PosTex0CoverageRect, PosTex0CoverageRectTile: pos(0) uv0(1) cov(2) rect(3)
	OUT_DATA(7) = IN_ATTRIBUTE(3);
  #elif NOESIS_VERTEX_FORMAT == 11 || NOESIS_VERTEX_FORMAT == 12
	// PosTex0Tex1Rect, PosTex0Tex1RectTile: pos(0) uv0(1) uv1(2) rect(3)
	OUT_DATA(7) = IN_ATTRIBUTE(3);
  #elif NOESIS_VERTEX_FORMAT == 14
	// PosColorTex1Rect: pos(0) color(1) uv1(2) rect(3)
	OUT_DATA(7) = IN_ATTRIBUTE(3);
  #elif NOESIS_VERTEX_FORMAT == 15
	// PosColorTex0RectImagePos: pos(0) color(1) uv0(2) rect(3)
	OUT_DATA(7) = IN_ATTRIBUTE(3);
  #endif
#endif

#ifdef HAS_TILE
  // Tile slot: always one after rect.
  #if NOESIS_VERTEX_FORMAT == 4
	// PosTex0RectTile: pos(0) uv0(1) rect(2) tile(3)
	OUT_DATA(8) = IN_ATTRIBUTE(3);
  #elif NOESIS_VERTEX_FORMAT == 8
	// PosTex0CoverageRectTile: pos(0) uv0(1) cov(2) rect(3) tile(4)
	OUT_DATA(8) = IN_ATTRIBUTE(4);
  #elif NOESIS_VERTEX_FORMAT == 12
	// PosTex0Tex1RectTile: pos(0) uv0(1) uv1(2) rect(3) tile(4)
	OUT_DATA(8) = IN_ATTRIBUTE(4);
  #endif
#endif

#ifdef HAS_IMAGE_POSITION
	// Format 15: pos(0) color(1) uv0(2) rect(3) imagePos(4)
	OUT_DATA(9) = IN_ATTRIBUTE(4);
#endif

MAIN_END
