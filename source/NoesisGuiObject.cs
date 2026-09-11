// 3D GUI panel in the world: a Noesis UI on a depth-tested quad in the scene, mouse-picked by
// raycast. A normal ObjectMeshDynamic quad shows a RenderTarget colour texture that Noesis
// renders into off-screen each frame; ray-quad intersection is pure managed math.
// The material noesis/materials/noesis_gui_mesh.basemat draws the panel: texture slot 0 is the
// panel colour and the "billboard" state toggles billboarding.

using System;

using Unigine;

#if UNIGINE_DOUBLE
using Vec3 = Unigine.dvec3;
using Mat4 = Unigine.dmat4;
#else
using Vec3 = Unigine.vec3;
using Mat4 = Unigine.mat4;
#endif

namespace UnigineApp
{
	public sealed class NoesisGuiObject
	{
		public const int MOUSE_STANDARD = 0;
		public const int MOUSE_VIRTUAL = 1;

		private string xamlPath = "";
		private float physicalWidth = 1.0f;
		private float physicalHeight = 1.0f;
		private int screenWidth = 1024;
		private int screenHeight = 1024;
		private bool depthTest = true;
		private bool billboard = false;
		private float controlDistance = 1.0f;
		private int mouseMode = MOUSE_STANDARD;

		private Vec3 manualP0;
		private Vec3 manualP1;
		private int manualButtons = 0;

		private bool prevLeftPressed = false;
		private bool prevRightPressed = false;
		private bool prevMiddlePressed = false;

		private Noesis.View view;
		private Texture colorTexture;
		private Texture depthTexture;
		private RenderTarget renderTarget;
		private Material surfaceMaterial;
		private ObjectMeshDynamic node;
		private bool viewReady = false;

		private NoesisDataContext dataCtx = null; // non-owning

		public NoesisGuiObject()
		{
			NoesisIntegration.Get()?.RegisterWorldView(this);
		}

		public void SetXaml(string xaml) { xamlPath = xaml; }
		public string GetXaml() { return xamlPath; }

		public void SetPhysicalSize(float width, float height)
		{
			physicalWidth = width;
			physicalHeight = height;
		}
		public float GetPhysicalWidth() { return physicalWidth; }
		public float GetPhysicalHeight() { return physicalHeight; }

		public void SetScreenSize(int width, int height) { screenWidth = width; screenHeight = height; }
		public int GetScreenWidth() { return screenWidth; }
		public int GetScreenHeight() { return screenHeight; }

		public void SetDepthTest(bool test) { depthTest = test; }
		public bool IsDepthTest() { return depthTest; }

		public void SetBillboard(bool value)
		{
			billboard = value;
			surfaceMaterial?.SetState("billboard", billboard ? 1 : 0);
		}
		public bool IsBillboard() { return billboard; }

		public void SetControlDistance(float distance) { controlDistance = distance; }
		public float GetControlDistance() { return controlDistance; }

		public void SetMouseMode(int mode) { mouseMode = mode; }
		public int GetMouseMode() { return mouseMode; }

		public void SetMouse(Vec3 p0, Vec3 p1, int buttons)
		{
			manualP0 = p0;
			manualP1 = p1;
			manualButtons = buttons;
		}
		public Vec3 GetManualP0() { return manualP0; }
		public Vec3 GetManualP1() { return manualP1; }
		public int GetManualButtons() { return manualButtons; }

		public Node GetNode() { EnsureNode(); return node; }
		public bool IsViewReady() { return viewReady; }
		public Noesis.View GetNoesisView() { return view; }

		public void SetDataContext(NoesisDataContext ctx)
		{
			dataCtx = ctx;
			if (viewReady && view != null)
				view.Content.DataContext = ctx;
		}

		// Lazily builds the view, render target, quad and material. Called from Update.
		public void InitializeRender()
		{
			if (viewReady || string.IsNullOrEmpty(xamlPath))
				return;

			Noesis.FrameworkElement root = Noesis.GUI.LoadXaml(xamlPath) as Noesis.FrameworkElement;
			if (root == null)
			{
				Log.Error("NoesisGuiObject: failed to load XAML '{0}'\n", xamlPath);
				return;
			}

			view = Noesis.GUI.CreateView(root);
			view.SetFlags(Noesis.RenderFlags.PPAA);
			view.SetTessellationMaxPixelError(Noesis.TessellationMaxPixelError.HighQuality);
			view.SetSize(screenWidth, screenHeight);

			NoesisIntegration integration = NoesisIntegration.Get();
			if (integration == null)
				return;

			view.Renderer.Init(integration.GetNoesisRenderDevice());

			colorTexture = new Texture();
			colorTexture.Create2D(screenWidth, screenHeight, Texture.FORMAT_RGBA8,
				Texture.FORMAT_USAGE_RENDER | Texture.FORMAT_MIPMAPS
				| Texture.SAMPLER_FILTER_TRILINEAR | Texture.SAMPLER_ANISOTROPY_16
				| Texture.SAMPLER_WRAP_CLAMP);

			// Noesis clips view content with the stencil buffer, so the offscreen target needs a
			// depth-stencil attachment; without it every stencilled draw is rejected and the RT
			// stays empty.
			depthTexture = new Texture();
			depthTexture.Create2D(screenWidth, screenHeight, Texture.FORMAT_D24S8, Texture.FORMAT_USAGE_RENDER);

			renderTarget = new RenderTarget();
			renderTarget.BindColorTexture(0, colorTexture);
			renderTarget.BindDepthTexture(depthTexture);

			EnsureNode();

			surfaceMaterial = Materials.FindMaterialByPath("noesis/materials/noesis_gui_mesh.basemat");
			if (surfaceMaterial != null)
			{
				surfaceMaterial = surfaceMaterial.Inherit();
				surfaceMaterial.SetState("billboard", billboard ? 1 : 0);
				surfaceMaterial.SetTexture(0, colorTexture); // panel colour = RT texture
				node.SetMaterial(surfaceMaterial, 0);
			}
			else
			{
				Log.Error("NoesisGuiObject: material not found: noesis/materials/noesis_gui_mesh.basemat\n");
			}

			if (dataCtx != null)
				view.Content.DataContext = dataCtx;

			viewReady = true;
		}

		// Off-screen render of the Noesis view into the panel texture. Called each frame.
		public void RenderOffscreen()
		{
			if (!viewReady)
				return;

			view.Update(Game.Time);

			RenderState.SaveState();
			RenderState.ClearStates();
			RenderState.SetViewport(0, 0, screenWidth, screenHeight);
			renderTarget.Enable();
			RenderState.ClearBuffer(RenderState.BUFFER_COLOR | RenderState.BUFFER_DEPTH | RenderState.BUFFER_STENCIL,
				new vec4(0.0f, 0.0f, 0.0f, 0.0f));

			Noesis.Renderer renderer = view.Renderer;
			renderer.UpdateRenderTree();
			renderer.RenderOffscreen();
			renderer.Render(false, false);

			renderTarget.Disable();
			RenderState.RestoreState();

			colorTexture.CreateMipmaps();
		}

		private void EnsureNode()
		{
			if (node == null)
				BuildQuad();
		}

		private void BuildQuad()
		{
			float halfW = physicalWidth * 0.5f;
			float halfH = physicalHeight * 0.5f;

			Mesh mesh = new Mesh();
			int surface = mesh.AddSurface("gui");

			// Quad in the local XY plane facing +Z. The offscreen colour texture is rendered
			// upright, but Unigine samples a render-target texture on geometry with a flipped V
			// origin, so V is mirrored here (bottom edge -> V=0, top edge -> V=1) to show upright.
			mesh.AddVertex(new vec3(-halfW, -halfH, 0.0f), surface); mesh.AddTexCoord0(new vec2(0.0f, 0.0f), surface); mesh.AddNormal(new vec3(0.0f, 0.0f, 1.0f), surface);
			mesh.AddVertex(new vec3( halfW, -halfH, 0.0f), surface); mesh.AddTexCoord0(new vec2(1.0f, 0.0f), surface); mesh.AddNormal(new vec3(0.0f, 0.0f, 1.0f), surface);
			mesh.AddVertex(new vec3( halfW,  halfH, 0.0f), surface); mesh.AddTexCoord0(new vec2(1.0f, 1.0f), surface); mesh.AddNormal(new vec3(0.0f, 0.0f, 1.0f), surface);
			mesh.AddVertex(new vec3(-halfW,  halfH, 0.0f), surface); mesh.AddTexCoord0(new vec2(0.0f, 1.0f), surface); mesh.AddNormal(new vec3(0.0f, 0.0f, 1.0f), surface);

			mesh.AddCIndices(new int[] { 0, 1, 2, 0, 2, 3 }, surface);
			mesh.AddTIndices(new int[] { 0, 1, 2, 0, 2, 3 }, surface);
			mesh.CreateBounds();

			node = new ObjectMeshDynamic(mesh);
		}

		// Ray-quad intersection. p0/p1 are the world-space ray endpoints.
		public bool GetIntersection(Vec3 worldP0, Vec3 worldP1, out Vec3 worldHit, out vec4 texcoord)
		{
			worldHit = new Vec3(0.0, 0.0, 0.0);
			texcoord = new vec4(0.0f, 0.0f, 0.0f, 0.0f);

			if (node == null)
				return false;

			// Quad world transform: camera-facing at the node origin for a billboard.
			Mat4 transform;
			Mat4 itransform;
			if (billboard)
			{
				Player player = Engine.MainPlayer;
				if (player == null)
					return false;
				transform = MathLib.Translate(node.WorldTransform.GetColumn3(3)) * MathLib.Rotation(player.WorldTransform);
				itransform = MathLib.Inverse(transform);
			}
			else
			{
				transform = node.WorldTransform;
				itransform = node.IWorldTransform;
			}

			// World ray -> quad-local space.
			vec3 p0 = ToVec3(itransform * worldP0);
			vec3 p1 = ToVec3(itransform * worldP1);

			// Intersect the local z=0 plane (plane = (0,0,1,0) -> distance is just .z).
			float denom = p0.z - p1.z;
			if (MathLib.Abs(denom) < MathLib.EPSILON)
				return false;
			float dist = p0.z / denom;
			if (dist < 0.0f || dist > 1.0f)
				return false;

			vec3 point = p0 + (p1 - p0) * dist;
			float x = point.x / physicalWidth + 0.5f;
			float y = point.y / physicalHeight + 0.5f;
			if (x < 0.0f || x > 1.0f || y < 0.0f || y > 1.0f)
				return false;

			worldHit = transform * new Vec3(point.x, point.y, point.z);
			texcoord = new vec4(x, y, 0.0f, 0.0f);
			return true;
		}

		public bool ForwardMouse(int px, int py, int wheel, bool left, bool right, bool middle)
		{
			if (!viewReady)
				return false;

			bool consumed = view.MouseMove(px, py);

			if (wheel != 0)
				consumed |= view.MouseWheel(px, py, wheel);
			if (left && !prevLeftPressed)
				consumed |= view.MouseButtonDown(px, py, Noesis.MouseButton.Left);
			else if (!left && prevLeftPressed)
				consumed |= view.MouseButtonUp(px, py, Noesis.MouseButton.Left);
			if (right && !prevRightPressed)
				consumed |= view.MouseButtonDown(px, py, Noesis.MouseButton.Right);
			else if (!right && prevRightPressed)
				consumed |= view.MouseButtonUp(px, py, Noesis.MouseButton.Right);
			if (middle && !prevMiddlePressed)
				consumed |= view.MouseButtonDown(px, py, Noesis.MouseButton.Middle);
			else if (!middle && prevMiddlePressed)
				consumed |= view.MouseButtonUp(px, py, Noesis.MouseButton.Middle);

			prevLeftPressed = left;
			prevRightPressed = right;
			prevMiddlePressed = middle;
			return consumed;
		}

		public void ForwardMouseLeave()
		{
			if (!viewReady)
				return;
			if (!prevLeftPressed && !prevRightPressed && !prevMiddlePressed)
				return;

			if (prevLeftPressed)   view.MouseButtonUp(-1, -1, Noesis.MouseButton.Left);
			if (prevRightPressed)  view.MouseButtonUp(-1, -1, Noesis.MouseButton.Right);
			if (prevMiddlePressed) view.MouseButtonUp(-1, -1, Noesis.MouseButton.Middle);

			prevLeftPressed = false;
			prevRightPressed = false;
			prevMiddlePressed = false;
		}

		public void ForwardKeyDown(Noesis.Key key) { if (viewReady) view.KeyDown(key); }
		public void ForwardKeyUp(Noesis.Key key)   { if (viewReady) view.KeyUp(key); }
		public void ForwardChar(uint code)         { if (viewReady) view.Char(code); }

		public void Shutdown()
		{
			NoesisIntegration.Get()?.UnregisterWorldView(this);
			if (view != null)
			{
				view.Renderer?.Shutdown();
				view = null;
			}
			node?.DeleteLater();
			node = null;
		}

		private static vec3 ToVec3(Vec3 v)
		{
			return new vec3((float)v.x, (float)v.y, (float)v.z);
		}
	}
}
