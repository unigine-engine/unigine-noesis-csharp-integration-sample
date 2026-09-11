// Noesis vertex-format tables and shader-define generation. The numeric tables are fixed by
// the Noesis 3.2 batch/shader/vertex enums; the shader-define logic selects the features so
// the Unigine shader (noesis/shaders/noesis_gui.*) compiles the right variant.

using Unigine;

namespace UnigineApp
{
	public static class NoesisShader
	{
		// Attribute component types (float / uchar / ushort).
		private const int F = MeshDynamic.TYPE_FLOAT;
		private const int U8 = MeshDynamic.TYPE_UCHAR;
		private const int U16 = MeshDynamic.TYPE_USHORT;

		// shader index (Noesis::Shader::Count == 53) -> vertex type index.
		private static readonly byte[] VERTEX_FOR_SHADER =
		{
			0, 0, 0, 1, 2, 2, 2, 3, 4, 4, 4, 4, 5, 6, 6, 6, 7, 8, 8, 8, 8, 9, 10, 10, 10, 11, 12, 12, 12,
			12, 9, 10, 10, 10, 11, 12, 12, 12, 12, 13, 14, 14, 14, 15, 16, 16, 16, 16, 17, 18, 19, 13, 20
		};

		// vertex type index (Noesis::Shader::Vertex::Count == 21) -> vertex format index.
		private static readonly byte[] FORMAT_FOR_VERTEX =
		{
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 9, 10, 11, 12, 13, 10, 14, 15
		};

		// vertex format index (Noesis::Shader::Vertex::Format::Count == 16) -> vertex byte size.
		private static readonly byte[] SIZE_FOR_FORMAT =
		{
			8, 12, 16, 24, 40, 16, 20, 28, 44, 20, 24, 32, 48, 28, 28, 44
		};

		// Attribute layout per vertex format. Order MUST match Noesis vertex packing.
		public static readonly MeshDynamic.Attribute[][] VERTEX_FORMAT_TABLE =
		{
			Attr((0, F, 2)),                                                 // 0: Pos
			Attr((0, F, 2), (8, U8, 4)),                                     // 1: PosColor
			Attr((0, F, 2), (8, F, 2)),                                      // 2: PosTex0
			Attr((0, F, 2), (8, F, 2), (16, U16, 4)),                        // 3: PosTex0Rect
			Attr((0, F, 2), (8, F, 2), (16, U16, 4), (24, F, 4)),            // 4: PosTex0RectTile
			Attr((0, F, 2), (8, U8, 4), (12, F, 1)),                         // 5: PosColorCoverage
			Attr((0, F, 2), (8, F, 2), (16, F, 1)),                          // 6: PosTex0Coverage
			Attr((0, F, 2), (8, F, 2), (16, F, 1), (20, U16, 4)),            // 7: PosTex0CoverageRect
			Attr((0, F, 2), (8, F, 2), (16, F, 1), (20, U16, 4), (28, F, 4)),// 8: PosTex0CoverageRectTile
			Attr((0, F, 2), (8, U8, 4), (12, F, 2)),                         // 9: PosColorTex1
			Attr((0, F, 2), (8, F, 2), (16, F, 2)),                          // 10: PosTex0Tex1
			Attr((0, F, 2), (8, F, 2), (16, F, 2), (24, U16, 4)),            // 11: PosTex0Tex1Rect
			Attr((0, F, 2), (8, F, 2), (16, F, 2), (24, U16, 4), (32, F, 4)),// 12: PosTex0Tex1RectTile
			Attr((0, F, 2), (8, U8, 4), (12, F, 2), (20, F, 2)),             // 13: PosColorTex0Tex1
			Attr((0, F, 2), (8, U8, 4), (12, F, 2), (20, U16, 4)),           // 14: PosColorTex1Rect
			Attr((0, F, 2), (8, U8, 4), (12, F, 2), (20, U16, 4), (28, F, 4))// 15: PosColorTex0RectImagePos
		};

		private static MeshDynamic.Attribute[] Attr(params (int offset, int type, int size)[] items)
		{
			MeshDynamic.Attribute[] result = new MeshDynamic.Attribute[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				result[i].offset = items[i].offset;
				result[i].type = items[i].type;
				result[i].size = items[i].size;
			}
			return result;
		}

		public static byte GetVertexFormatIndex(byte shaderV)
		{
			return FORMAT_FOR_VERTEX[VERTEX_FOR_SHADER[shaderV]];
		}

		public static byte GetVertexSize(byte shaderV)
		{
			return SIZE_FOR_FORMAT[GetVertexFormatIndex(shaderV)];
		}

		public static string GetShaderDefines(Noesis.Shader.Enum shaderType)
		{
			string defines = "HLSL_WRAPPER,";
			if (Render.API == (int)Render.RENDER_API.API_VULKAN)
				defines += "VULKAN,";
			defines += "NOESIS_VERTEX_FORMAT=" + GetVertexFormatIndex((byte)shaderType) + ",";

			switch (shaderType)
			{
				case Noesis.Shader.Enum.RGBA:          return defines + "EFFECT_RGBA,";
				case Noesis.Shader.Enum.Mask:          return defines + "EFFECT_MASK,";
				case Noesis.Shader.Enum.Clear:         return defines + "EFFECT_CLEAR,";
				case Noesis.Shader.Enum.Upsample:      return defines + "EFFECT_UPSAMPLE,HAS_COLOR,HAS_UV0,HAS_UV1,";
				case Noesis.Shader.Enum.Downsample:    return defines + "EFFECT_DOWNSAMPLE,HAS_UV0,HAS_UV1,HAS_UV2,HAS_UV3,";
				case Noesis.Shader.Enum.Shadow:        return defines + "EFFECT_SHADOW,PAINT_SOLID,HAS_COLOR,HAS_UV1,HAS_RECT,";
				case Noesis.Shader.Enum.Blur:          return defines + "EFFECT_BLUR,PAINT_SOLID,HAS_COLOR,HAS_UV1,";
				case Noesis.Shader.Enum.Custom_Effect: return defines + "EFFECT_CUSTOM,PAINT_SOLID,HAS_COLOR,";
				default: break;
			}

			bool hasColor = false, hasUv0 = false, hasUv1 = false;
			bool hasSt1 = false, hasCoverage = false, hasRect = false, hasTile = false;

			// Paint kind + pattern modifier.
			switch (shaderType)
			{
				case Noesis.Shader.Enum.Path_Solid:
				case Noesis.Shader.Enum.Path_AA_Solid:
				case Noesis.Shader.Enum.SDF_Solid:
				case Noesis.Shader.Enum.SDF_LCD_Solid:
				case Noesis.Shader.Enum.Opacity_Solid:
					defines += "PAINT_SOLID,";
					hasColor = true;
					break;

				case Noesis.Shader.Enum.Path_Linear:
				case Noesis.Shader.Enum.Path_AA_Linear:
				case Noesis.Shader.Enum.SDF_Linear:
				case Noesis.Shader.Enum.SDF_LCD_Linear:
				case Noesis.Shader.Enum.Opacity_Linear:
					defines += "PAINT_LINEAR,";
					hasUv0 = true;
					break;

				case Noesis.Shader.Enum.Path_Radial:
				case Noesis.Shader.Enum.Path_AA_Radial:
				case Noesis.Shader.Enum.SDF_Radial:
				case Noesis.Shader.Enum.SDF_LCD_Radial:
				case Noesis.Shader.Enum.Opacity_Radial:
					defines += "PAINT_RADIAL,";
					hasUv0 = true;
					break;

				case Noesis.Shader.Enum.Path_Pattern:
				case Noesis.Shader.Enum.Path_AA_Pattern:
				case Noesis.Shader.Enum.SDF_Pattern:
				case Noesis.Shader.Enum.SDF_LCD_Pattern:
				case Noesis.Shader.Enum.Opacity_Pattern:
					defines += "PAINT_PATTERN,";
					hasUv0 = true;
					break;

				case Noesis.Shader.Enum.Path_Pattern_Clamp:
				case Noesis.Shader.Enum.Path_AA_Pattern_Clamp:
				case Noesis.Shader.Enum.SDF_Pattern_Clamp:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_Clamp:
				case Noesis.Shader.Enum.Opacity_Pattern_Clamp:
					defines += "PAINT_PATTERN,CLAMP_PATTERN,";
					hasUv0 = hasRect = true;
					break;

				case Noesis.Shader.Enum.Path_Pattern_Repeat:
				case Noesis.Shader.Enum.Path_AA_Pattern_Repeat:
				case Noesis.Shader.Enum.SDF_Pattern_Repeat:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_Repeat:
				case Noesis.Shader.Enum.Opacity_Pattern_Repeat:
					defines += "PAINT_PATTERN,REPEAT_PATTERN,";
					hasUv0 = hasRect = hasTile = true;
					break;

				case Noesis.Shader.Enum.Path_Pattern_MirrorU:
				case Noesis.Shader.Enum.Path_AA_Pattern_MirrorU:
				case Noesis.Shader.Enum.SDF_Pattern_MirrorU:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_MirrorU:
				case Noesis.Shader.Enum.Opacity_Pattern_MirrorU:
					defines += "PAINT_PATTERN,MIRRORU_PATTERN,";
					hasUv0 = hasRect = hasTile = true;
					break;

				case Noesis.Shader.Enum.Path_Pattern_MirrorV:
				case Noesis.Shader.Enum.Path_AA_Pattern_MirrorV:
				case Noesis.Shader.Enum.SDF_Pattern_MirrorV:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_MirrorV:
				case Noesis.Shader.Enum.Opacity_Pattern_MirrorV:
					defines += "PAINT_PATTERN,MIRRORV_PATTERN,";
					hasUv0 = hasRect = hasTile = true;
					break;

				case Noesis.Shader.Enum.Path_Pattern_Mirror:
				case Noesis.Shader.Enum.Path_AA_Pattern_Mirror:
				case Noesis.Shader.Enum.SDF_Pattern_Mirror:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_Mirror:
				case Noesis.Shader.Enum.Opacity_Pattern_Mirror:
					defines += "PAINT_PATTERN,MIRROR_PATTERN,";
					hasUv0 = hasRect = hasTile = true;
					break;

				default:
					// Unknown paint shader — fall back to PAINT_SOLID.
					defines += "PAINT_SOLID,";
					hasColor = true;
					break;
			}

			// Effect kind.
			switch (shaderType)
			{
				case Noesis.Shader.Enum.Path_Solid:
				case Noesis.Shader.Enum.Path_Linear:
				case Noesis.Shader.Enum.Path_Radial:
				case Noesis.Shader.Enum.Path_Pattern:
				case Noesis.Shader.Enum.Path_Pattern_Clamp:
				case Noesis.Shader.Enum.Path_Pattern_Repeat:
				case Noesis.Shader.Enum.Path_Pattern_MirrorU:
				case Noesis.Shader.Enum.Path_Pattern_MirrorV:
				case Noesis.Shader.Enum.Path_Pattern_Mirror:
					defines += "EFFECT_PATH,";
					break;

				case Noesis.Shader.Enum.Path_AA_Solid:
				case Noesis.Shader.Enum.Path_AA_Linear:
				case Noesis.Shader.Enum.Path_AA_Radial:
				case Noesis.Shader.Enum.Path_AA_Pattern:
				case Noesis.Shader.Enum.Path_AA_Pattern_Clamp:
				case Noesis.Shader.Enum.Path_AA_Pattern_Repeat:
				case Noesis.Shader.Enum.Path_AA_Pattern_MirrorU:
				case Noesis.Shader.Enum.Path_AA_Pattern_MirrorV:
				case Noesis.Shader.Enum.Path_AA_Pattern_Mirror:
					defines += "EFFECT_PATH_AA,";
					hasCoverage = true;
					break;

				case Noesis.Shader.Enum.SDF_Solid:
				case Noesis.Shader.Enum.SDF_Linear:
				case Noesis.Shader.Enum.SDF_Radial:
				case Noesis.Shader.Enum.SDF_Pattern:
				case Noesis.Shader.Enum.SDF_Pattern_Clamp:
				case Noesis.Shader.Enum.SDF_Pattern_Repeat:
				case Noesis.Shader.Enum.SDF_Pattern_MirrorU:
				case Noesis.Shader.Enum.SDF_Pattern_MirrorV:
				case Noesis.Shader.Enum.SDF_Pattern_Mirror:
					defines += "EFFECT_SDF,";
					hasUv1 = hasSt1 = true;
					break;

				case Noesis.Shader.Enum.SDF_LCD_Solid:
				case Noesis.Shader.Enum.SDF_LCD_Linear:
				case Noesis.Shader.Enum.SDF_LCD_Radial:
				case Noesis.Shader.Enum.SDF_LCD_Pattern:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_Clamp:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_Repeat:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_MirrorU:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_MirrorV:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_Mirror:
					defines += "EFFECT_SDF_LCD,";
					hasUv1 = hasSt1 = true;
					break;

				case Noesis.Shader.Enum.Opacity_Solid:
				case Noesis.Shader.Enum.Opacity_Linear:
				case Noesis.Shader.Enum.Opacity_Radial:
				case Noesis.Shader.Enum.Opacity_Pattern:
				case Noesis.Shader.Enum.Opacity_Pattern_Clamp:
				case Noesis.Shader.Enum.Opacity_Pattern_Repeat:
				case Noesis.Shader.Enum.Opacity_Pattern_MirrorU:
				case Noesis.Shader.Enum.Opacity_Pattern_MirrorV:
				case Noesis.Shader.Enum.Opacity_Pattern_Mirror:
					defines += "EFFECT_OPACITY,";
					hasUv1 = true;
					break;

				default:
					defines += "EFFECT_PATH,";
					break;
			}

			if (hasColor)    defines += "HAS_COLOR,";
			if (hasUv0)      defines += "HAS_UV0,";
			if (hasUv1)      defines += "HAS_UV1,";
			if (hasSt1)      defines += "HAS_ST1,";
			if (hasCoverage) defines += "HAS_COVERAGE,";
			if (hasRect)     defines += "HAS_RECT,";
			if (hasTile)     defines += "HAS_TILE,";
			return defines;
		}
	}
}
