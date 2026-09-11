// Noesis RenderDevice implemented on Unigine's low-level renderer: it creates textures and
// render targets, maps vertex/index buffers, compiles per-format shaders and draws the Noesis
// batches (Batch/Shader/RenderState/SamplerState/Tile/TextureFormat/DeviceCaps/UniformData).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unigine;

namespace UnigineApp
{
	public sealed class NoesisRenderDevice : Noesis.RenderDevice
	{
		private const string VERTEX_SHADER_PATH = "noesis/shaders/noesis_gui.vert";
		private const string PIXEL_SHADER_PATH = "noesis/shaders/noesis_gui.frag";

		// Unmanaged staging buffers handed to Noesis via MapVertices/MapIndices, then
		// uploaded from in DrawBatch. Grown on demand, freed in ReleaseResources.
		private IntPtr vertexBuffer = IntPtr.Zero;
		private int vertexBufferBytes = 0;
		private IntPtr indexBuffer = IntPtr.Zero;
		private int indexBufferBytes = 0;

		private readonly MeshDynamic[] meshDynamicPerFormat =
			new MeshDynamic[NoesisShader.VERTEX_FORMAT_TABLE.Length];

		private readonly Dictionary<byte, Shader> shaderCache = new Dictionary<byte, Shader>();

		private NoesisRenderTarget currentRenderTarget = null;

		private readonly int colorFormat;
		private readonly int depthStencilFormat;

		private Noesis.DeviceCaps caps;

		public NoesisRenderDevice(int colorFormat, int depthStencilFormat)
		{
			this.colorFormat = colorFormat;
			this.depthStencilFormat = depthStencilFormat;
			caps.LinearRendering = false;
			caps.DepthRangeZeroToOne = true;
		}

		public override Noesis.DeviceCaps Caps => caps;

		// ---- Texture / render-target creation --------------------------------------------

		// data is a pointer to an array of numLevels mip pointers (native const void**),
		// or IntPtr.Zero for a texture created without initial data (e.g. the glyph atlas).
		public override Noesis.Texture CreateTexture(string label, uint width, uint height,
			uint numLevels, Noesis.TextureFormat noesisFormat, IntPtr data)
		{
			int textureFormat = Texture.FORMAT_RGBA8;
			bool useAlpha = true;
			switch (noesisFormat)
			{
				case Noesis.TextureFormat.R8:    textureFormat = Texture.FORMAT_R8;    break;
				case Noesis.TextureFormat.RGBA8: textureFormat = Texture.FORMAT_RGBA8; break;
				case Noesis.TextureFormat.RGBX8:
					textureFormat = Texture.FORMAT_RGBA8;
					useAlpha = false;
					break;
				default: textureFormat = Texture.FORMAT_R8; break;
			}

			Texture tex = new Texture();
			tex.Create2D((int)width, (int)height, textureFormat);
			tex.DebugName = label;

			NoesisTexture noesisTex = new NoesisTexture(tex, useAlpha);

			if (data != IntPtr.Zero)
			{
				uint w = width, h = height;
				for (uint level = 0; level < numLevels; level++)
				{
					IntPtr levelData = Marshal.ReadIntPtr(data, (int)level * IntPtr.Size);
					UpdateTexture(noesisTex, level, 0, 0, w, h, levelData);
					w >>= 1;
					h >>= 1;
				}
			}
			return noesisTex;
		}

		public override Noesis.RenderTarget CreateRenderTarget(string label, uint width,
			uint height, uint sampleCount, bool needsStencil)
		{
			Texture color = new Texture();
			color.Create2D((int)width, (int)height, colorFormat,
				Texture.SAMPLER_FILTER_LINEAR | Texture.FORMAT_USAGE_RENDER);
			color.DebugName = label + ": Noesis Color AA";

			Texture depthStencil = null;
			if (needsStencil)
			{
				depthStencil = new Texture();
				depthStencil.Create2D((int)width, (int)height, depthStencilFormat, Texture.FORMAT_USAGE_RENDER);
				depthStencil.DebugName = label + ": Noesis Depth Stencil";
			}
			return CreateRenderTargetImpl(label, width, height, color, depthStencil);
		}

		public override Noesis.RenderTarget CloneRenderTarget(string label, Noesis.RenderTarget surface)
		{
			NoesisRenderTarget rt = (NoesisRenderTarget)surface;
			Texture color = rt.GetColorTexture();
			Texture depth = rt.GetDepthTexture();
			return CreateRenderTargetImpl(label, (uint)color.GetWidth(0), (uint)color.GetHeight(0), color, depth);
		}

		private Noesis.RenderTarget CreateRenderTargetImpl(string label, uint width, uint height,
			Texture colorAa, Texture depthStencil)
		{
			Texture srv = new Texture();
			srv.Create2D((int)width, (int)height, colorFormat, Texture.SAMPLER_FILTER_LINEAR);
			srv.DebugName = label + ": Noesis SRV";
			return new NoesisRenderTarget(srv, colorAa, depthStencil);
		}

		// ---- Vertex / index buffer mapping ------------------------------------------------

		public override IntPtr MapVertices(uint bytes)
		{
			EnsureVertexCapacity((int)bytes);
			return vertexBufferBytes == 0 ? IntPtr.Zero : vertexBuffer;
		}

		public override void UnmapVertices() { } // upload happens in DrawBatch

		public override IntPtr MapIndices(uint bytes)
		{
			EnsureIndexCapacity((int)bytes);
			return indexBufferBytes == 0 ? IntPtr.Zero : indexBuffer;
		}

		public override void UnmapIndices() { } // upload happens in DrawBatch

		private void EnsureVertexCapacity(int bytes)
		{
			if (bytes <= vertexBufferBytes)
				return;
			vertexBuffer = vertexBuffer == IntPtr.Zero
				? Marshal.AllocHGlobal(bytes)
				: Marshal.ReAllocHGlobal(vertexBuffer, (IntPtr)bytes);
			vertexBufferBytes = bytes;
		}

		private void EnsureIndexCapacity(int bytes)
		{
			if (bytes <= indexBufferBytes)
				return;
			indexBuffer = indexBuffer == IntPtr.Zero
				? Marshal.AllocHGlobal(bytes)
				: Marshal.ReAllocHGlobal(indexBuffer, (IntPtr)bytes);
			indexBufferBytes = bytes;
		}

		// ---- Render target / offscreen scopes ---------------------------------------------

		public override void SetRenderTarget(Noesis.RenderTarget surface)
		{
			currentRenderTarget = (NoesisRenderTarget)surface;
			if (currentRenderTarget != null)
				currentRenderTarget.BindTextures();
		}

		public override void BeginOffscreenRender() { RenderState.SaveState(); RenderState.ClearStates(); }
		public override void EndOffscreenRender()   { RenderState.RestoreState(); }
		public override void BeginOnscreenRender()  { RenderState.SaveState(); RenderState.ClearStates(); }
		public override void EndOnscreenRender()    { RenderState.RestoreState(); }

		public override void BeginTile(Noesis.RenderTarget surface, Noesis.Tile tile) { }
		public override void EndTile(Noesis.RenderTarget surface) { }

		public override void ResolveRenderTarget(Noesis.RenderTarget surface, Noesis.Tile[] tiles)
		{
			NoesisRenderTarget rt = (NoesisRenderTarget)surface;
			Texture src = rt.GetColorTexture();
			Texture dst = rt.GetShaderResourceTexture();

			for (int i = 0; i < tiles.Length; i++)
			{
				Noesis.Tile tile = tiles[i];
				ivec3 offset = new ivec3((int)tile.X, dst.GetHeight(0) - (int)(tile.Y + tile.Height), 0);
				dst.CopyRegion(src, offset, 0, offset, 0, (int)tile.Width, (int)tile.Height, 1);
			}

			if (currentRenderTarget != null)
			{
				currentRenderTarget.UnbindTextures();
				currentRenderTarget = null;
			}
		}

		// ---- Texture updates --------------------------------------------------------------

		public override void UpdateTexture(Noesis.Texture texture, uint level, uint x, uint y,
			uint width, uint height, IntPtr data)
		{
			// Backend uses single-mip dynamic textures (glyph atlas); level 0 only.
			if (level != 0)
			{
				Log.Error("NoesisRenderDevice.UpdateTexture: multi-mip textures not supported\n");
				return;
			}

			NoesisTexture tex = (NoesisTexture)texture;
			Texture texturePtr = tex.GetTexturePtr();
			int imageFormat = texturePtr.ImageFormat;

			Image image = new Image();
			image.Create2D((int)width, (int)height, imageFormat, 1);
			CopyUnmanaged(data, image.GetPixels2D(0), (int)image.PixelsSize);
			texturePtr.SetImage2D(image, (int)x, (int)y);
		}

		// ---- Batch drawing ----------------------------------------------------------------

		public override void DrawBatch(ref Noesis.Batch batch)
		{
			if (batch.NumIndices == 0 || vertexBuffer == IntPtr.Zero || indexBuffer == IntPtr.Zero)
				return;

			byte shaderV = (byte)batch.Shader.Index;
			Shader shader = GetOrCompileShader(shaderV);
			if (shader == null)
				return;


			// Pick the MeshDynamic dedicated to this batch's vertex format. Sharing one
			// MeshDynamic across formats breaks on Vulkan.
			byte formatIndex = NoesisShader.GetVertexFormatIndex(shaderV);
			MeshDynamic meshDynamic = meshDynamicPerFormat[formatIndex];
			if (meshDynamic == null)
			{
				meshDynamic = new MeshDynamic(MeshDynamic.USAGE_DYNAMIC_ALL);
				meshDynamic.SetVertexFormat(NoesisShader.VERTEX_FORMAT_TABLE[formatIndex]);
				meshDynamicPerFormat[formatIndex] = meshDynamic;
			}

			// Upload only the slice of vertices this batch needs (no copy — offset the pointer).
			IntPtr vertexSrc = IntPtr.Add(vertexBuffer, (int)batch.VertexOffset);
			meshDynamic.SetVertexArray(vertexSrc, (int)batch.NumVertices);

			// Convert uint16 -> int for the slice this batch consumes. SetIndicesArray(int[])
			// takes its count from Length, so the array must be exactly NumIndices long.
			int[] indices = new int[batch.NumIndices];
			for (uint i = 0; i < batch.NumIndices; i++)
			{
				short raw = Marshal.ReadInt16(indexBuffer, (int)((batch.StartIndex + i) * 2));
				indices[i] = (ushort)raw;
			}
			meshDynamic.SetIndicesArray(indices);

			SetShaderTexture(0, batch.Pattern, batch.PatternSampler, "Pattern");
			SetShaderTexture(1, batch.Ramps,   batch.RampsSampler,   "Ramps");
			SetShaderTexture(2, batch.Image,   batch.ImageSampler,   "Image");
			SetShaderTexture(3, batch.Glyphs,  batch.GlyphsSampler,  "Glyphs");
			SetShaderTexture(4, batch.Shadow,  batch.ShadowSampler,  "Shadow");

			SetVertexUniforms(shader, ref batch);
			SetPixelUniforms(shader, (Noesis.Shader.Enum)shaderV, ref batch);

			ApplyRenderState(batch.RenderState, batch.StencilRef);

			RenderState.Shader = shader;
			RenderState.FlushStates();

			meshDynamic.Bind();
			meshDynamic.FlushVertex();
			meshDynamic.FlushIndices();
			meshDynamic.RenderSurface(MeshDynamic.MODE_TRIANGLES, 0, 0, (int)batch.NumIndices);
			meshDynamic.Unbind();

			RenderState.ClearTextures();
		}

		private Shader GetOrCompileShader(byte shaderType)
		{
			Shader cached;
			if (shaderCache.TryGetValue(shaderType, out cached))
				return cached;

			string defines = NoesisShader.GetShaderDefines((Noesis.Shader.Enum)shaderType);

			Shader shader = new Shader();
			if (!shader.CompileVertFrag(VERTEX_SHADER_PATH, PIXEL_SHADER_PATH, defines))
			{
				Log.Error("[Noesis] Shader compile failed for type {0}\nDefines: {1}\n", shaderType, defines);
				return null;
			}
			shaderCache[shaderType] = shader;
			return shader;
		}

		private void SetShaderTexture(int slot, Noesis.Texture texture, Noesis.SamplerState sampler, string name)
		{
			if (texture == null)
				return;

			int samplerFlags = 0;

			if (sampler.WrapMode == Noesis.WrapMode.ClampToEdge)
				samplerFlags |= Texture.SAMPLER_WRAP_CLAMP;
			else if (sampler.WrapMode == Noesis.WrapMode.ClampToZero)
				samplerFlags |= Texture.SAMPLER_WRAP_BORDER;

			if (sampler.MinMagFilter == Noesis.MinMagFilter.Nearest)
			{
				int[] filterFlags = { Texture.SAMPLER_FILTER_POINT, Texture.SAMPLER_FILTER_POINT, Texture.SAMPLER_FILTER_LINEAR };
				samplerFlags |= filterFlags[(int)sampler.MipFilter];
			}
			else if (sampler.MinMagFilter == Noesis.MinMagFilter.Linear)
			{
				int[] filterFlags = { Texture.SAMPLER_FILTER_BILINEAR, Texture.SAMPLER_FILTER_BILINEAR, Texture.SAMPLER_FILTER_TRILINEAR };
				samplerFlags |= filterFlags[(int)sampler.MipFilter];
			}

			Texture shaderTexture = ((NoesisTexture)texture).GetTexturePtr();
			shaderTexture.SamplerFlags = samplerFlags;
			shaderTexture.DebugName = name;

			RenderState.SetTexture(RenderState.BIND_FRAGMENT, slot, shaderTexture);
		}

		private void SetVertexUniforms(Shader shader, ref Noesis.Batch batch)
		{
			// VS cbuffer b0: projectionMtx (16 floats, column-major float4x4).
			IntPtr proj = batch.VertexUniform0.Values;
			if (proj != IntPtr.Zero)
			{
				float[] f = ReadFloats(proj, 16);
				// Noesis provides the projection column-major. mat4(col0, col1, col2, col3) stores
				// each vec4 as a contiguous column (column N = f[N*4 .. N*4+3]), and
				// SetParameterFloat4x4 uploads it with column-major HLSL packing (no transpose).
				// Depth convention is D3D-style here because the device reports
				// DeviceCaps.DepthRangeZeroToOne (see constructor).
				mat4 projMtx = new mat4(
					new vec4(f[0],  f[1],  f[2],  f[3]),
					new vec4(f[4],  f[5],  f[6],  f[7]),
					new vec4(f[8],  f[9],  f[10], f[11]),
					new vec4(f[12], f[13], f[14], f[15]));
				shader.SetParameterFloat4x4("projectionMtx", projMtx);
			}

			// VS cbuffer b1: textureSize (2 floats, only for SDF variants).
			IntPtr ts = batch.VertexUniform1.Values;
			if (ts != IntPtr.Zero)
			{
				float[] f = ReadFloats(ts, 2);
				shader.SetParameterFloat2("textureSize", new vec2(f[0], f[1]));
			}
		}

		private void SetPixelUniforms(Shader shader, Noesis.Shader.Enum shaderType, ref Noesis.Batch batch)
		{
			IntPtr ps0Ptr = batch.PixelUniform0.Values;
			IntPtr ps1Ptr = batch.PixelUniform1.Values;

			// EFFECT_RGBA: float4 rgba.
			if (shaderType == Noesis.Shader.Enum.RGBA && ps0Ptr != IntPtr.Zero)
			{
				float[] ps0 = ReadFloats(ps0Ptr, 4);
				shader.SetParameterFloat4("rgba", new vec4(ps0[0], ps0[1], ps0[2], ps0[3]));
				return;
			}

			// PAINT_RADIAL: float4 radialGrad0, float3 radialGrad1.
			switch (shaderType)
			{
				case Noesis.Shader.Enum.Path_Radial:
				case Noesis.Shader.Enum.Path_AA_Radial:
				case Noesis.Shader.Enum.SDF_Radial:
				case Noesis.Shader.Enum.SDF_LCD_Radial:
				case Noesis.Shader.Enum.Opacity_Radial:
					if (ps0Ptr != IntPtr.Zero)
					{
						float[] ps0 = ReadFloats(ps0Ptr, 7);
						shader.SetParameterFloat4("radialGrad0", new vec4(ps0[0], ps0[1], ps0[2], ps0[3]));
						shader.SetParameterFloat3("radialGrad1", new vec3(ps0[4], ps0[5], ps0[6]));
					}
					return;
				default:
					break;
			}

			// PAINT_LINEAR: float opacity.
			switch (shaderType)
			{
				case Noesis.Shader.Enum.Path_Linear:
				case Noesis.Shader.Enum.Path_AA_Linear:
				case Noesis.Shader.Enum.SDF_Linear:
				case Noesis.Shader.Enum.SDF_LCD_Linear:
				case Noesis.Shader.Enum.Opacity_Linear:
					if (ps0Ptr != IntPtr.Zero)
						shader.SetParameterFloat("opacity", ReadFloats(ps0Ptr, 1)[0]);
					return;
				default:
					break;
			}

			// PAINT_PATTERN family: float opacity.
			switch (shaderType)
			{
				case Noesis.Shader.Enum.Path_Pattern:
				case Noesis.Shader.Enum.Path_Pattern_Clamp:
				case Noesis.Shader.Enum.Path_Pattern_Repeat:
				case Noesis.Shader.Enum.Path_Pattern_MirrorU:
				case Noesis.Shader.Enum.Path_Pattern_MirrorV:
				case Noesis.Shader.Enum.Path_Pattern_Mirror:
				case Noesis.Shader.Enum.Path_AA_Pattern:
				case Noesis.Shader.Enum.Path_AA_Pattern_Clamp:
				case Noesis.Shader.Enum.Path_AA_Pattern_Repeat:
				case Noesis.Shader.Enum.Path_AA_Pattern_MirrorU:
				case Noesis.Shader.Enum.Path_AA_Pattern_MirrorV:
				case Noesis.Shader.Enum.Path_AA_Pattern_Mirror:
				case Noesis.Shader.Enum.SDF_Pattern:
				case Noesis.Shader.Enum.SDF_Pattern_Clamp:
				case Noesis.Shader.Enum.SDF_Pattern_Repeat:
				case Noesis.Shader.Enum.SDF_Pattern_MirrorU:
				case Noesis.Shader.Enum.SDF_Pattern_MirrorV:
				case Noesis.Shader.Enum.SDF_Pattern_Mirror:
				case Noesis.Shader.Enum.SDF_LCD_Pattern:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_Clamp:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_Repeat:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_MirrorU:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_MirrorV:
				case Noesis.Shader.Enum.SDF_LCD_Pattern_Mirror:
				case Noesis.Shader.Enum.Opacity_Pattern:
				case Noesis.Shader.Enum.Opacity_Pattern_Clamp:
				case Noesis.Shader.Enum.Opacity_Pattern_Repeat:
				case Noesis.Shader.Enum.Opacity_Pattern_MirrorU:
				case Noesis.Shader.Enum.Opacity_Pattern_MirrorV:
				case Noesis.Shader.Enum.Opacity_Pattern_Mirror:
					if (ps0Ptr != IntPtr.Zero)
						shader.SetParameterFloat("opacity", ReadFloats(ps0Ptr, 1)[0]);
					return;
				default:
					break;
			}

			// EFFECT_SHADOW: Buffer2 = float4 shadowColor, float2 shadowOffset, float blend.
			if (shaderType == Noesis.Shader.Enum.Shadow && ps1Ptr != IntPtr.Zero)
			{
				float[] ps1 = ReadFloats(ps1Ptr, 7);
				shader.SetParameterFloat4("shadowColor", new vec4(ps1[0], ps1[1], ps1[2], ps1[3]));
				shader.SetParameterFloat2("shadowOffset", new vec2(ps1[4], ps1[5]));
				shader.SetParameterFloat("blend", ps1[6]);
				return;
			}

			// EFFECT_BLUR: Buffer2 = float blend.
			if (shaderType == Noesis.Shader.Enum.Blur && ps1Ptr != IntPtr.Zero)
			{
				shader.SetParameterFloat("blend", ReadFloats(ps1Ptr, 1)[0]);
				return;
			}

			// PAINT_SOLID and everything else: no PS uniforms needed.
		}

		private void ApplyRenderState(Noesis.RenderState state, byte stencilRef)
		{
			// Blend.
			if (!state.ColorEnable)
			{
				// Stencil-only pass: preserve color.
				RenderState.SetBlendFunc(RenderState.BLEND_ZERO, RenderState.BLEND_ONE);
			}
			else
			{
				switch (state.BlendMode)
				{
					case Noesis.BlendMode.Src:
						RenderState.SetBlendFunc(RenderState.BLEND_ONE, RenderState.BLEND_ZERO);
						break;
					case Noesis.BlendMode.SrcOver:
						RenderState.SetBlendFunc(RenderState.BLEND_ONE, RenderState.BLEND_ONE_MINUS_SRC_ALPHA);
						break;
					case Noesis.BlendMode.SrcOver_Multiply:
						RenderState.SetBlendFunc(RenderState.BLEND_DEST_COLOR, RenderState.BLEND_ONE_MINUS_SRC_ALPHA);
						break;
					case Noesis.BlendMode.SrcOver_Screen:
						RenderState.SetBlendFunc(RenderState.BLEND_ONE, RenderState.BLEND_ONE_MINUS_SRC_COLOR);
						break;
					case Noesis.BlendMode.SrcOver_Additive:
						RenderState.SetBlendFunc(RenderState.BLEND_ONE, RenderState.BLEND_ONE);
						break;
					case Noesis.BlendMode.SrcOver_Dual:
						RenderState.SetBlendFunc(RenderState.BLEND_ONE, RenderState.BLEND_ONE_MINUS_SRC1_COLOR);
						break;
					default:
						RenderState.SetBlendFunc(RenderState.BLEND_ONE, RenderState.BLEND_ONE_MINUS_SRC_ALPHA);
						break;
				}
			}

			// Wireframe.
			RenderState.PolygonFill = state.Wireframe ? RenderState.FILL_WIREFRAME : RenderState.FILL_SOLID;

			// Depth write always off for Noesis.
			RenderState.DepthWrite = false;

			// Stencil.
			switch (state.StencilMode)
			{
				case Noesis.StencilMode.Disabled:
					RenderState.StencilFunc = RenderState.STENCIL_NONE;
					break;
				case Noesis.StencilMode.Equal_Keep:
					RenderState.StencilFunc = RenderState.STENCIL_EQUAL;
					RenderState.StencilPass = RenderState.STENCIL_KEEP;
					RenderState.StencilRef = stencilRef;
					break;
				case Noesis.StencilMode.Equal_Incr:
					RenderState.StencilFunc = RenderState.STENCIL_EQUAL;
					RenderState.StencilPass = RenderState.STENCIL_INCR;
					RenderState.StencilRef = stencilRef;
					break;
				case Noesis.StencilMode.Equal_Decr:
					RenderState.StencilFunc = RenderState.STENCIL_EQUAL;
					RenderState.StencilPass = RenderState.STENCIL_DECR;
					RenderState.StencilRef = stencilRef;
					break;
				case Noesis.StencilMode.Clear:
					// Equivalent to STENCIL_OP_ZERO: write 0 to stencil.
					RenderState.StencilFunc = RenderState.STENCIL_ALWAYS;
					RenderState.StencilPass = RenderState.STENCIL_REPLACE;
					RenderState.StencilRef = 0;
					break;
				case Noesis.StencilMode.Disabled_ZTest:
					RenderState.StencilFunc = RenderState.STENCIL_NONE;
					RenderState.DepthFunc = RenderState.DEPTH_GEQUAL;
					break;
				case Noesis.StencilMode.Equal_Keep_ZTest:
					RenderState.StencilFunc = RenderState.STENCIL_EQUAL;
					RenderState.StencilPass = RenderState.STENCIL_KEEP;
					RenderState.StencilRef = stencilRef;
					RenderState.DepthFunc = RenderState.DEPTH_GEQUAL;
					break;
				default:
					RenderState.StencilFunc = RenderState.STENCIL_NONE;
					break;
			}
		}

		// ---- Helpers ----------------------------------------------------------------------

		private static float[] ReadFloats(IntPtr src, int count)
		{
			float[] buffer = new float[count];
			Marshal.Copy(src, buffer, 0, count);
			return buffer;
		}

		private static void CopyUnmanaged(IntPtr src, IntPtr dst, int bytes)
		{
			if (src == IntPtr.Zero || dst == IntPtr.Zero || bytes <= 0)
				return;
			byte[] bounce = new byte[bytes];
			Marshal.Copy(src, bounce, 0, bytes);
			Marshal.Copy(bounce, 0, dst, bytes);
		}

		// Called from NoesisIntegration.Shutdown to free all GPU resources.
		public void ReleaseResources()
		{
			foreach (MeshDynamic mesh in meshDynamicPerFormat)
				mesh?.Dispose();
			foreach (Shader shader in shaderCache.Values)
				shader?.Dispose();
			shaderCache.Clear();

			if (vertexBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(vertexBuffer); vertexBuffer = IntPtr.Zero; vertexBufferBytes = 0; }
			if (indexBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(indexBuffer); indexBuffer = IntPtr.Zero; indexBufferBytes = 0; }
		}
	}
}
