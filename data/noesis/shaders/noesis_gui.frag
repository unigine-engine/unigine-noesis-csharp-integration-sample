#include <core/materials/shaders/render/common.h>

#ifdef EFFECT_SDF || EFFECT_SDF_LCD
	#define SDF_SCALE 7.96875
	#define SDF_BIAS 0.50196078431
	#define SDF_AA_FACTOR 0.65
	#define SDF_BASE_MIN 0.125
	#define SDF_BASE_MAX 0.25
	#define SDF_BASE_DEV -0.65
#endif

INIT_TEXTURE(0, TEX_PATTERN)
INIT_TEXTURE(1, TEX_RAMPS)
INIT_TEXTURE(2, TEX_IMAGE)
INIT_TEXTURE(3, TEX_GLYPHS)
INIT_TEXTURE(4, TEX_SHADOW)

#ifdef EFFECT_RGBA || PAINT_LINEAR || PAINT_PATTERN || PAINT_RADIAL || EFFECT_BLUR || EFFECT_SHADOW
CBUFFER(Buffer0)
	#ifdef EFFECT_RGBA
		UNIFORM float4 rgba;
	#endif

	#ifdef PAINT_LINEAR || PAINT_PATTERN
		UNIFORM float opacity;
	#endif

	#ifdef PAINT_RADIAL
		UNIFORM float4 radialGrad0;
		UNIFORM float3 radialGrad1;
	#endif

	#ifdef EFFECT_BLUR
		UNIFORM float blend;
	#endif

	#ifdef EFFECT_SHADOW
		UNIFORM float4 shadowColor;
		UNIFORM float2 shadowOffset;
		UNIFORM float blend;
	#endif
END
#endif

// Input interpolants — slots must match VERTEX_OUT in noesis_gui.vert exactly.
STRUCT(FRAGMENT_IN)
	INIT_POSITION
#ifdef HAS_COLOR
	MODIFIER_NOINTERPOLATION INIT_IN(half4, 0)
#endif
#ifdef HAS_UV0
	INIT_IN(float2, 1)
#endif
#ifdef HAS_UV1
	INIT_IN(float2, 2)
#endif
#ifdef HAS_UV2
	INIT_IN(float2, 3)
#endif
#ifdef HAS_UV3
	INIT_IN(float2, 4)
#endif
#ifdef HAS_ST1
	INIT_IN(float4, 5)
#endif
#ifdef HAS_COVERAGE
	INIT_IN(half, 6)
#endif
#ifdef HAS_RECT
	MODIFIER_NOINTERPOLATION INIT_IN(float4, 7)
#endif
#ifdef HAS_TILE
	MODIFIER_NOINTERPOLATION INIT_IN(float4, 8)
#endif
END

STRUCT_FRAG_BEGIN
	INIT_COLOR(RGBA8)
#ifdef EFFECT_SDF_LCD
	INIT_MRT_DUAL_BLENDING(RGBA8, 1)
#endif
STRUCT_FRAG_END

float4 main_brush(float2 uv);

MAIN_FRAG_BEGIN(FRAGMENT_IN)

	/////////////////////////////////////////////////////
	// Fetch paint color and opacity
	/////////////////////////////////////////////////////
	#ifdef PAINT_SOLID
		half4 paint = IN_DATA(0);
		half opacity_ = 1.0;

	#elif PAINT_LINEAR
		half4 paint = (half4)TEXTURE(TEX_RAMPS, IN_DATA(1));
		half opacity_ = (half)opacity;

	#elif PAINT_RADIAL
		half dd = half(radialGrad1.x * IN_DATA(1).x - radialGrad1.y * IN_DATA(1).y);
		half u = half(radialGrad0.x * IN_DATA(1).x + radialGrad0.y * IN_DATA(1).y + radialGrad0.z *
			sqrt(IN_DATA(1).x * IN_DATA(1).x + IN_DATA(1).y * IN_DATA(1).y - dd * dd));
		half4 paint = (half4)TEXTURE(TEX_RAMPS, half2(u, radialGrad1.z));
		half opacity_ = (half)radialGrad0.w;

	#elif PAINT_PATTERN
		#ifdef CUSTOM_PATTERN
			half4 paint = main_brush(IN_DATA(1));
		#elif CLAMP_PATTERN
			float inside = all(IN_DATA(1) == clamp(IN_DATA(1), IN_DATA(7).xy, IN_DATA(7).zw));
			half4 paint = half4(inside * TEXTURE(TEX_PATTERN, IN_DATA(1)));
		#elif REPEAT_PATTERN
			float2 uv = (IN_DATA(1) - IN_DATA(8).xy) / IN_DATA(8).zw;
			uv = frac(uv);
			uv = uv * IN_DATA(8).zw + IN_DATA(8).xy;
			float inside = all(uv == clamp(uv, IN_DATA(7).xy, IN_DATA(7).zw));
			half4 paint = half4(inside * TEXTURE_GRAD(TEX_PATTERN, uv, ddx(IN_DATA(1)), ddy(IN_DATA(1))));
		#elif MIRRORU_PATTERN
			float2 uv = (IN_DATA(1) - IN_DATA(8).xy) / IN_DATA(8).zw;
			uv.x = abs(uv.x - 2.0 * floor((uv.x - 1.0) / 2.0) - 2.0);
			uv.y = frac(uv.y);
			uv = uv * IN_DATA(8).zw + IN_DATA(8).xy;
			float inside = all(uv == clamp(uv, IN_DATA(7).xy, IN_DATA(7).zw));
			half4 paint = half4(inside * TEXTURE_GRAD(TEX_PATTERN, uv, ddx(IN_DATA(1)), ddy(IN_DATA(1))));
		#elif MIRRORV_PATTERN
			float2 uv = (IN_DATA(1) - IN_DATA(8).xy) / IN_DATA(8).zw;
			uv.x = frac(uv.x);
			uv.y = abs(uv.y - 2.0 * floor((uv.y - 1.0) / 2.0) - 2.0);
			uv = uv * IN_DATA(8).zw + IN_DATA(8).xy;
			float inside = all(uv == clamp(uv, IN_DATA(7).xy, IN_DATA(7).zw));
			half4 paint = half4(inside * TEXTURE_GRAD(TEX_PATTERN, uv, ddx(IN_DATA(1)), ddy(IN_DATA(1))));
		#elif MIRROR_PATTERN
			float2 uv = (IN_DATA(1) - IN_DATA(8).xy) / IN_DATA(8).zw;
			uv = abs(uv - 2.0 * floor((uv - 1.0) / 2.0) - 2.0);
			uv = uv * IN_DATA(8).zw + IN_DATA(8).xy;
			float inside = all(uv == clamp(uv, IN_DATA(7).xy, IN_DATA(7).zw));
			half4 paint = half4(inside * TEXTURE_GRAD(TEX_PATTERN, uv, ddx(IN_DATA(1)), ddy(IN_DATA(1))));
		#else
			half4 paint = (half4)TEXTURE(TEX_PATTERN, IN_DATA(1));
		#endif
		half opacity_ = (half)opacity;
	#endif

	/////////////////////////////////////////////////////
	// Apply selected effect
	/////////////////////////////////////////////////////
	#ifdef EFFECT_RGBA
		OUT_COLOR = (half4)rgba;

	#elif EFFECT_MASK
		OUT_COLOR = half4(1, 1, 1, 1);

	#elif EFFECT_CLEAR
		OUT_COLOR = half4(0, 0, 0, 0);

	#elif EFFECT_PATH
		OUT_COLOR = opacity_ * paint;

	#elif EFFECT_PATH_AA
		OUT_COLOR = (opacity_ * IN_DATA(6)) * paint;

	#elif EFFECT_OPACITY
		OUT_COLOR = (half4)TEXTURE(TEX_IMAGE, IN_DATA(2)) * (opacity_ * paint.a);

	#elif EFFECT_SHADOW
		half2 uv = (half2)clamp(IN_DATA(2) - shadowOffset, IN_DATA(7).xy, IN_DATA(7).zw);
		half alpha = (half)lerp(TEXTURE(TEX_IMAGE, uv).a, TEXTURE(TEX_SHADOW, uv).a, blend);
		half4 img = (half4)TEXTURE(TEX_IMAGE, clamp(IN_DATA(2), IN_DATA(7).xy, IN_DATA(7).zw));
		OUT_COLOR = (img + (1.0 - img.a) * ((half4)shadowColor * alpha)) * (opacity_ * paint.a);

	#elif EFFECT_BLUR
		OUT_COLOR = half4(lerp(TEXTURE(TEX_IMAGE, IN_DATA(2)), TEXTURE(TEX_SHADOW, IN_DATA(2)), blend)) * (opacity_ * paint.a);

	#elif EFFECT_SDF
		half4 color = (half4)TEXTURE(TEX_GLYPHS, IN_DATA(2));
		half distance = SDF_SCALE * (color.r - SDF_BIAS);

		#if 1
			half2 grad = (half2)ddx(IN_DATA(5).xy);
		#else
			half2 Jdx = ddx(IN_DATA(5));
			half2 Jdy = ddy(IN_DATA(5));
			half2 distGrad = half2(ddx(distance), ddy(distance));
			half distGradLen2 = dot(distGrad, distGrad);
			distGrad = distGradLen2 < 0.0001 ? half2(0.7071, 0.7071) : distGrad * half(rsqrt(distGradLen2));
			half2 grad = half2(distGrad.x * Jdx.x + distGrad.y * Jdy.x, distGrad.x * Jdx.y + distGrad.y * Jdy.y);
		#endif

		half gradLen = (half)length(grad);
		half scale = 1.0 / gradLen;
		half base = SDF_BASE_DEV * (1.0 - (clamp(scale, SDF_BASE_MIN, SDF_BASE_MAX) - SDF_BASE_MIN) / (SDF_BASE_MAX - SDF_BASE_MIN));
		half range = SDF_AA_FACTOR * gradLen;
		half alpha = smoothstep(base - range, base + range, distance);

		OUT_COLOR = (alpha * opacity_) * paint;

	#elif EFFECT_SDF_LCD
		half2 grad = ddx(IN_DATA(5).xy);
		half2 offset = grad * IN_DATA(5).zw;

		half4 red = TEXTURE(TEX_GLYPHS, IN_DATA(2) - offset);
		half4 green = TEXTURE(TEX_GLYPHS, IN_DATA(2));
		half4 blue = TEXTURE(TEX_GLYPHS, IN_DATA(2) + offset);
		half3 distance = SDF_SCALE * (half3(red.r, green.r, blue.r) - SDF_BIAS);

		half gradLen = (half)length(grad);
		half scale = 1.0 / gradLen;
		half base = SDF_BASE_DEV * (1.0 - (clamp(scale, SDF_BASE_MIN, SDF_BASE_MAX) - SDF_BASE_MIN) / (SDF_BASE_MAX - SDF_BASE_MIN));
		half range = SDF_AA_FACTOR * gradLen;
		half3 alpha = smoothstep(base - range, base + range, distance);

		OUT_COLOR = half4(opacity_ * paint.rgb * alpha.rgb, alpha.g);
		OUT_MRT(1) = half4((opacity_ * paint.a) * alpha.rgb, alpha.g);

	#elif EFFECT_DOWNSAMPLE
		OUT_COLOR = half4
		(
			TEXTURE(TEX_PATTERN, IN_DATA(1)) +
			TEXTURE(TEX_PATTERN, IN_DATA(2)) +
			TEXTURE(TEX_PATTERN, IN_DATA(3)) +
			TEXTURE(TEX_PATTERN, IN_DATA(4))
		) * 0.25;

	#elif EFFECT_UPSAMPLE
		OUT_COLOR = (half4)lerp(TEXTURE(TEX_IMAGE, IN_DATA(2)), TEXTURE(TEX_PATTERN, IN_DATA(1)), IN_DATA(0).a);

	#elif EFFECT_CUSTOM
		OUT_COLOR = opacity_ * paint;

	#else
		#error "EFFECT not defined"
	#endif

MAIN_FRAG_END
