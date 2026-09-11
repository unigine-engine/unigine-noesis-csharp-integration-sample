// Orchestrator singleton: owns the render device, providers, fonts/theme, the view registry,
// per-frame update, overlay rendering and mouse/keyboard routing.

using System;
using System.Collections.Generic;

using Unigine;

#if UNIGINE_DOUBLE
using Vec3 = Unigine.dvec3;
#else
using Vec3 = Unigine.vec3;
#endif

namespace UnigineApp
{
	public sealed class NoesisIntegration
	{
		private struct KeyMapping
		{
			public Input.KEY UnigineKey;
			public Noesis.Key NoesisKey;
			public KeyMapping(Input.KEY u, Noesis.Key n) { UnigineKey = u; NoesisKey = n; }
		}

		private static readonly KeyMapping[] KEY_MAP =
		{
			new KeyMapping(Input.KEY.LEFT,      Noesis.Key.Left),
			new KeyMapping(Input.KEY.RIGHT,     Noesis.Key.Right),
			new KeyMapping(Input.KEY.UP,        Noesis.Key.Up),
			new KeyMapping(Input.KEY.DOWN,      Noesis.Key.Down),
			new KeyMapping(Input.KEY.ENTER,     Noesis.Key.Return),
			new KeyMapping(Input.KEY.ESC,       Noesis.Key.Escape),
			new KeyMapping(Input.KEY.TAB,       Noesis.Key.Tab),
			new KeyMapping(Input.KEY.BACKSPACE, Noesis.Key.Back),
			new KeyMapping(Input.KEY.DELETE,    Noesis.Key.Delete),
			new KeyMapping(Input.KEY.HOME,      Noesis.Key.Home),
			new KeyMapping(Input.KEY.END,       Noesis.Key.End),
			new KeyMapping(Input.KEY.SPACE,     Noesis.Key.Space),
		};

		private static NoesisIntegration instance;
		public static NoesisIntegration Get() { return instance; }

		private NoesisRenderDevice renderDevice;
		private readonly List<NoesisView> activeViews = new List<NoesisView>();
		private readonly List<NoesisGuiObject> worldViews = new List<NoesisGuiObject>();
		private readonly List<NoesisGuiObject> createdObjects = new List<NoesisGuiObject>();
		private readonly List<NoesisDataContext> createdContexts = new List<NoesisDataContext>();
		private NoesisGuiObject lastActiveWorldView = null;
		private EngineWindowViewport mainWindow;

		private readonly EventConnections eventConnections = new EventConnections();

		private bool leftMousePressed = false;
		private bool rightMousePressed = false;
		private bool middleMousePressed = false;

		private Input.MOUSE_HANDLE prevMouseHandle = Input.MOUSE_HANDLE.GRAB;

		private bool initialized = false;

		private string applicationResources = "";
		private float defaultFontSize = 14.0f;
		private string fontFallbacks = "";
		private int glyphCacheSize = 2048;
		private NoesisFontProvider fontProvider; // non-owning (SDK owns via SetFontProvider)

		public NoesisIntegration() { instance = this; }

		public NoesisRenderDevice GetNoesisRenderDevice() { return renderDevice; }

		// ---- Lifecycle --------------------------------------------------------------------

		public bool Init()
		{
			Noesis.Log.SetLogCallback(NoesisLog);

			Noesis.GUI.SetLicense("", "");
			Noesis.GUI.Init();

			System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
				typeof(NoesisApp.Interaction).TypeHandle);

			string dataPath = Engine.DataPath.Replace('\\', '/');
			Noesis.GUI.SetXamlProvider(new NoesisXamlProvider(dataPath));
			Noesis.GUI.SetTextureProvider(new NoesisTextureProvider(dataPath));

			fontProvider = new NoesisFontProvider(dataPath);
			Noesis.GUI.SetFontProvider(fontProvider);

			renderDevice = new NoesisRenderDevice(Texture.FORMAT_RGBA8, Texture.FORMAT_D24S8);

			initialized = true;

			prevMouseHandle = ControlsApp.MouseHandle;
			if (prevMouseHandle == Input.MOUSE_HANDLE.USER)
				prevMouseHandle = Input.MOUSE_HANDLE.GRAB;

			SetGlyphCacheSize(glyphCacheSize);
			ApplyFontDefaults();

			Engine.EventEndInputUpdate.Connect(eventConnections, UpdateViews);

			mainWindow = WindowManager.MainWindow;
			if (mainWindow != null)
				mainWindow.EventFuncBeginRenderGui.Connect(eventConnections, (Gui gui) => RenderViews());
			else
				Log.Error("NoesisIntegration: no main window — render will not work\n");

			return true;
		}

		public void Shutdown()
		{
			
			eventConnections.DisconnectAll();
			
			mainWindow = null;

			foreach (NoesisView view in activeViews)
				view.Shutdown();
			activeViews.Clear();

			foreach (NoesisGuiObject obj in createdObjects)
				obj.Shutdown();
			createdObjects.Clear();
			worldViews.Clear();

			createdContexts.Clear();

			renderDevice?.ReleaseResources();
			renderDevice = null;

			if (initialized)
			{
				Input.MouseHandle = prevMouseHandle;
				Noesis.GUI.Shutdown();
				initialized = false;
			}
		}

		// ---- Settings ---------------------------------------------------------------------

		public void SetApplicationResources(string xamlPath)
		{
			applicationResources = xamlPath;
			if (initialized && !string.IsNullOrEmpty(applicationResources))
				Noesis.GUI.LoadApplicationResources(applicationResources);
		}
		public string GetApplicationResources() { return applicationResources; }

		public void SetDefaultFontSize(float size)
		{
			defaultFontSize = size;
			if (initialized)
				ApplyFontDefaults();
		}
		public float GetDefaultFontSize() { return defaultFontSize; }

		public void SetFontFallbacks(string fallbacks)
		{
			fontFallbacks = fallbacks;
			if (initialized)
				ApplyFontFallbacks();
		}
		public string GetFontFallbacks() { return fontFallbacks; }

		public void RegisterFont(string filePath)
		{
			if (fontProvider == null)
			{
				Log.Warning("NoesisIntegration.RegisterFont: called before Init()\n");
				return;
			}
			fontProvider.RegisterFace(filePath);
		}

		public void SetGlyphCacheSize(int size)
		{
			glyphCacheSize = size;
			if (initialized && renderDevice != null)
			{
				renderDevice.GlyphCacheWidth = (uint)size;
				renderDevice.GlyphCacheHeight = (uint)size;
			}
		}
		public int GetGlyphCacheSize() { return glyphCacheSize; }

		private void ApplyFontFallbacks()
		{
			// Split on ',' yields one empty entry for an empty list — filter those out so an
			// unset fallback list leaves the Noesis default alone instead of installing "".
			string[] names = fontFallbacks.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			if (names.Length > 0)
				Noesis.GUI.SetFontFallbacks(names);
		}

		private void ApplyFontDefaults()
		{
			Noesis.GUI.SetFontDefaultProperties(defaultFontSize,
				Noesis.FontWeight.Normal, Noesis.FontStretch.Normal, Noesis.FontStyle.Normal);
		}

		private static void NoesisLog(Noesis.LogLevel level, string channel, string message)
		{
			if (level >= Noesis.LogLevel.Error)
				Log.Error("[Noesis] {0}\n", message);
			else if (level == Noesis.LogLevel.Warning)
				Log.Warning("[Noesis] {0}\n", message);
			else
				Log.Message("[Noesis] {0}\n", message);
		}

		// ---- View / object / data-context registry ----------------------------------------

		public NoesisView CreateView(string xamlPath)
		{
			Noesis.FrameworkElement root = Noesis.GUI.LoadXaml(xamlPath) as Noesis.FrameworkElement;
			if (root == null)
			{
				Log.Error("NoesisIntegration: failed to load XAML '{0}'\n", xamlPath);
				return null;
			}

			Noesis.View iview = Noesis.GUI.CreateView(root);
			iview.SetFlags(Noesis.RenderFlags.PPAA);

			if (mainWindow != null)
			{
				ivec2 size = mainWindow.ClientRenderSize;
				iview.SetSize(size.x, size.y);
			}

			iview.Renderer.Init(renderDevice);

			NoesisView view = new NoesisView(iview, xamlPath);
			activeViews.Add(view);
			return view;
		}

		public bool DestroyView(NoesisView view)
		{
			int index = activeViews.IndexOf(view);
			if (index < 0)
				return false;
			activeViews[index].Shutdown();
			activeViews.RemoveAt(index);
			return true;
		}

		public NoesisGuiObject CreateObject(string xamlPath = "")
		{
			NoesisGuiObject obj = new NoesisGuiObject();
			if (!string.IsNullOrEmpty(xamlPath))
				obj.SetXaml(xamlPath);
			createdObjects.Add(obj);
			return obj;
		}

		public bool DestroyObject(NoesisGuiObject obj)
		{
			if (obj == null)
				return false;
			int index = createdObjects.IndexOf(obj);
			if (index < 0)
				return false;
			createdObjects.RemoveAt(index);
			obj.Shutdown();
			return true;
		}

		public NoesisDataContext CreateDataContext()
		{
			NoesisDataContext ctx = new NoesisDataContext();
			createdContexts.Add(ctx);
			return ctx;
		}

		public bool DestroyDataContext(NoesisDataContext ctx)
		{
			if (ctx == null)
				return false;
			return createdContexts.Remove(ctx);
		}

		public void RegisterWorldView(NoesisGuiObject view)
		{
			if (view != null && !worldViews.Contains(view))
				worldViews.Add(view);
		}

		public void UnregisterWorldView(NoesisGuiObject view)
		{
			worldViews.Remove(view);
			if (lastActiveWorldView == view)
				lastActiveWorldView = null;
		}

		// ---- Per-frame update (Engine.EventEndInputUpdate) ---------------------------------

		private void UpdateViews()
		{
			// Lazily build world panels (safe here — logic phase, before rendering).
			foreach (NoesisGuiObject worldView in worldViews)
				worldView.InitializeRender();

			bool grab = Input.MouseGrab;

			bool wantMouse = false;
			if (!grab)
			{
				wantMouse |= HandleMouseInput();
				wantMouse |= HandleWorldMouseInput();
				wantMouse |= IsUiCapturingMouse();
			}

			HandleKeyboardInput();

			double time = Game.Time;
			foreach (NoesisView view in activeViews)
			{
				if (view.IsEnabled())
					view.GetNoesisView().Update(time);
			}

			if (!grab)
				Input.MouseHandle = wantMouse ? Input.MOUSE_HANDLE.USER : prevMouseHandle;
		}

		private bool IsUiCapturingMouse()
		{
			foreach (NoesisView view in activeViews)
			{
				if (!view.IsEnabled())
					continue;
				Noesis.FrameworkElement content = view.GetNoesisView().Content;
				if (content != null && content.IsMouseCaptureWithin)
					return true;
			}

			foreach (NoesisGuiObject worldView in worldViews)
			{
				if (!worldView.IsViewReady())
					continue;
				Noesis.View iview = worldView.GetNoesisView();
				if (iview != null && iview.Content != null && iview.Content.IsMouseCaptureWithin)
					return true;
			}

			return false;
		}

		private bool HandleMouseInput()
		{
			if (Unigine.Console.Active)
				return false;

			ivec2 mousePos = Input.MousePosition;
			ivec2 clientPos = mainWindow != null ? mainWindow.ClientPosition : new ivec2(0, 0);
			ivec2 clientSize = mainWindow != null ? mainWindow.ClientRenderSize : new ivec2(0, 0);
			int cx = mousePos.x - clientPos.x;
			int cy = mousePos.y - clientPos.y;

			int wheel = Input.MouseWheel;
			bool left = Input.IsMouseButtonPressed(Input.MOUSE_BUTTON.LEFT);
			bool right = Input.IsMouseButtonPressed(Input.MOUSE_BUTTON.RIGHT);
			bool middle = Input.IsMouseButtonPressed(Input.MOUSE_BUTTON.MIDDLE);

			bool overContent = false;
			foreach (NoesisView view in activeViews)
			{
				if (!view.IsEnabled())
					continue;

				Noesis.View iview = view.GetNoesisView();

				ivec4 rect = view.IsRectFullWindow()
					? new ivec4(0, 0, clientSize.x, clientSize.y)
					: view.GetRect();
				bool inside = cx >= rect.x && cx < rect.x + rect.z
				           && cy >= rect.y && cy < rect.y + rect.w;

				if (!inside)
				{
					iview.MouseMove(-1, -1); // clear hover state
					continue;
				}

				int vx = cx - rect.x;
				int vy = cy - rect.y;

				overContent |= iview.MouseMove(vx, vy);

				if (wheel != 0)
					iview.MouseWheel(vx, vy, wheel);

				if (left && !leftMousePressed)
					iview.MouseButtonDown(vx, vy, Noesis.MouseButton.Left);
				else if (!left && leftMousePressed)
					iview.MouseButtonUp(vx, vy, Noesis.MouseButton.Left);

				if (right && !rightMousePressed)
					iview.MouseButtonDown(vx, vy, Noesis.MouseButton.Right);
				else if (!right && rightMousePressed)
					iview.MouseButtonUp(vx, vy, Noesis.MouseButton.Right);

				if (middle && !middleMousePressed)
					iview.MouseButtonDown(vx, vy, Noesis.MouseButton.Middle);
				else if (!middle && middleMousePressed)
					iview.MouseButtonUp(vx, vy, Noesis.MouseButton.Middle);
			}

			leftMousePressed = left;
			rightMousePressed = right;
			middleMousePressed = middle;
			return overContent;
		}

		private bool HandleWorldMouseInput()
		{
			if (worldViews.Count == 0 || Unigine.Console.Active)
				return false;

			Player player = Engine.MainPlayer;
			if (player == null)
				return false;

			ivec2 mousePos = Input.MousePosition;
			Vec3 stdP0, stdP1;
			player.GetDirectionFromMainWindow(out stdP0, out stdP1, mousePos.x, mousePos.y);

			bool stdLeft = Input.IsMouseButtonPressed(Input.MOUSE_BUTTON.LEFT);
			bool stdRight = Input.IsMouseButtonPressed(Input.MOUSE_BUTTON.RIGHT);
			bool stdMiddle = Input.IsMouseButtonPressed(Input.MOUSE_BUTTON.MIDDLE);
			int stdWheel = Input.MouseWheel;

			NoesisGuiObject hitView = null;
			float hitDistance = MathLib.INFINITY;
			vec4 hitUv = new vec4(0.0f, 0.0f, 0.0f, 0.0f);

			foreach (NoesisGuiObject wv in worldViews)
			{
				if (!wv.IsViewReady())
					continue;

				Vec3 wp0, wp1;
				if (wv.GetMouseMode() == NoesisGuiObject.MOUSE_VIRTUAL)
				{
					wp0 = wv.GetManualP0();
					wp1 = wv.GetManualP1();
				}
				else
				{
					wp0 = stdP0;
					Vec3 dirNorm = MathLib.Normalize(stdP1 - stdP0);
					wp1 = wp0 + dirNorm * wv.GetControlDistance();
				}

				Vec3 worldHit;
				vec4 texcoord;
				if (!wv.GetIntersection(wp0, wp1, out worldHit, out texcoord))
					continue;

				Visualizer.RenderPoint3D(worldHit, 0.01f, vec4.GREEN, false, 0.0f, false);

				float distance = (float)MathLib.Length(worldHit - wp0);
				if (distance < hitDistance)
				{
					hitDistance = distance;
					hitView = wv;
					hitUv = texcoord;
				}
			}

			bool consumed = false;
			foreach (NoesisGuiObject wv in worldViews)
			{
				if (wv != hitView)
				{
					wv.ForwardMouseLeave();
					continue;
				}

				int px = (int)(hitUv.x * wv.GetScreenWidth());
				int py = (int)((1.0f - hitUv.y) * wv.GetScreenHeight());

				bool left, right, middle;
				int wheel;
				if (wv.GetMouseMode() == NoesisGuiObject.MOUSE_VIRTUAL)
				{
					int btn = wv.GetManualButtons();
					left = (btn & 1) != 0;
					right = (btn & 2) != 0;
					middle = (btn & 4) != 0;
					wheel = 0;
				}
				else
				{
					left = stdLeft;
					right = stdRight;
					middle = stdMiddle;
					wheel = stdWheel;
				}

				consumed |= wv.ForwardMouse(px, py, wheel, left, right, middle);
				lastActiveWorldView = wv;
			}
			return consumed;
		}

		private void HandleKeyboardInput()
		{
			if (Unigine.Console.Active)
				return;

			foreach (KeyMapping m in KEY_MAP)
			{
				bool pressed = Input.IsKeyPressed(m.UnigineKey);
				bool up = Input.IsKeyUp(m.UnigineKey);
				if (!pressed && !up)
					continue;

				foreach (NoesisView view in activeViews)
				{
					if (!view.IsEnabled())
						continue;
					Noesis.View iview = view.GetNoesisView();
					if (pressed)
						iview.KeyDown(m.NoesisKey);
					else
						iview.KeyUp(m.NoesisKey);
				}

				if (lastActiveWorldView != null)
				{
					if (pressed)
						lastActiveWorldView.ForwardKeyDown(m.NoesisKey);
					else
						lastActiveWorldView.ForwardKeyUp(m.NoesisKey);
				}
			}
		}

		// ---- Rendering (EngineWindow.EventFuncBeginRenderGui) ------------------------------

		private void RenderViews()
		{
			// World panels render off-screen into their own targets (1 frame of latency for
			// the in-scene quad, which sampled the previous frame — fine for a sample).
			foreach (NoesisGuiObject worldView in worldViews)
				worldView.RenderOffscreen();

			if (activeViews.Count == 0)
				return;

			ivec2 client = mainWindow.ClientRenderSize;

			foreach (NoesisView view in activeViews)
			{
				if (!view.IsEnabled())
					continue;

				ivec4 rect = view.IsRectFullWindow()
					? new ivec4(0, 0, client.x, client.y)
					: view.GetRect();

				Noesis.View iview = view.GetNoesisView();
				iview.SetSize(rect.z, rect.w);

				Noesis.Renderer renderer = iview.Renderer;
				renderer.UpdateRenderTree();
				renderer.RenderOffscreen();

				RenderState.SaveState();
				RenderState.SetViewport(rect.x, rect.y, rect.z, rect.w);
				RenderState.SetScissorTest((float)rect.x, (float)rect.y, (float)rect.z, (float)rect.w);

				renderer.Render(false, false);

				RenderState.RestoreState();
			}
		}
	}
}
