// Noesis texture / render-target wrappers over Unigine.Texture / Unigine.RenderTarget.
// The Noesis.Texture getters (Width/Height/HasMipMaps/IsInverted/HasAlpha) are overridden as
// properties; NoesisRenderTarget holds the color, depth-stencil and shader-resource textures
// and binds them as the active render target.

using Unigine;

namespace UnigineApp
{
	public sealed class NoesisTexture : Noesis.Texture
	{
		private readonly Unigine.Texture ptr;
		private readonly uint width;
		private readonly uint height;
		private readonly uint numMipmaps;
		private readonly bool hasAlpha;
		private readonly bool useAlpha;

		public NoesisTexture(Unigine.Texture texture, bool useAlpha = true)
		{
			ptr = texture;
			width = (uint)texture.GetWidth(0);
			height = (uint)texture.GetHeight(0);
			numMipmaps = (uint)texture.NumMipmaps;
			hasAlpha = texture.Format == Unigine.Texture.FORMAT_RGBA8;
			this.useAlpha = useAlpha;
		}

		public Unigine.Texture GetTexturePtr() { return ptr; }

		public override uint Width => width;
		public override uint Height => height;
		public override bool HasMipMaps => numMipmaps > 1;
		public override bool IsInverted => false;
		public override bool HasAlpha => hasAlpha;
	}

	public sealed class NoesisRenderTarget : Noesis.RenderTarget
	{
		private readonly NoesisTexture texture;      // shader-resource-view wrapper
		private readonly Unigine.Texture colorOut;   // MSAA/color attachment
		private readonly Unigine.Texture depthStencilOut;
		private readonly Unigine.RenderTarget renderTarget;

		public NoesisRenderTarget(Unigine.Texture srv, Unigine.Texture colorOut, Unigine.Texture depthStencilOut)
		{
			texture = new NoesisTexture(srv);
			this.colorOut = colorOut;
			this.depthStencilOut = depthStencilOut;
			renderTarget = new Unigine.RenderTarget();
		}

		public Unigine.Texture GetColorTexture() { return colorOut; }
		public Unigine.Texture GetDepthTexture() { return depthStencilOut; }
		public Unigine.Texture GetShaderResourceTexture() { return texture.GetTexturePtr(); }

		public void BindTextures()
		{
			RenderState.SetTexture(RenderState.BIND_ALL, 0, texture.GetTexturePtr());
			renderTarget.BindColorTexture(0, colorOut);
			if (depthStencilOut != null)
				renderTarget.BindDepthTexture(depthStencilOut);
			renderTarget.Enable();
		}

		public void UnbindTextures()
		{
			renderTarget.Disable();
			renderTarget.UnbindAll();
			RenderState.SetTexture(RenderState.BIND_ALL, 0, null);
		}

		public override Noesis.Texture Texture => texture;
	}
}
