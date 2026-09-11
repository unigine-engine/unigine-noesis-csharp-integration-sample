// World logic: build the data context, the three Noesis UIs (3D panel + two overlays) and
// wire the engine reactions (sun rotation).

using Unigine;

#if UNIGINE_DOUBLE
using Mat4 = Unigine.dmat4;
#else
using Mat4 = Unigine.mat4;
#endif

namespace UnigineApp
{
	class AppWorldLogic : WorldLogic
	{
		private static readonly float[] TIME_PRESET_ANGLES = { 45.0f, 90.0f, 150.0f };

		private NoesisView overlay1;
		private NoesisView overlay2;
		private NoesisGuiObject objectGui;
		private NoesisDataContext dataContext;

		public override bool Init()
		{
			NoesisIntegration gui = NoesisIntegration.Get();
			if (gui == null)
				return true;

			dataContext = gui.CreateDataContext();
			dataContext.SetFloat("sun_angle_x", 45.0f);
			dataContext.SetInt("time_preset", 0);
			dataContext.SetString("font_family", "ui/fonts/#Muli");
			dataContext.Changed += OnDataContextChanged;

			// Trigger initial sun rotation.
			RotateSun(45.0f);

			// World object with GUI (3D surface). Set parameters before touching the node so
			// the quad is built at the right physical size.
			objectGui = gui.CreateObject("ui/world.xaml");
			if (objectGui != null)
			{
				objectGui.SetPhysicalSize(2.0f, 2.0f);
				objectGui.SetScreenSize(1024, 1024);
				objectGui.SetDepthTest(true);
				objectGui.SetBillboard(false);
				objectGui.SetControlDistance(5.0f);
				objectGui.SetDataContext(dataContext);
				objectGui.GetNode().WorldTransform = new Mat4(MathLib.RotateX(60.0f));
			}

			// Overlay pinned to a sub-rect (viewport-style).
			overlay1 = gui.CreateView("ui/overlay.xaml");
			if (overlay1 != null)
			{
				overlay1.SetRect(20, 20, 500, 300);
				overlay1.SetDataContext(dataContext);
			}

			// Full-window overlay (font showcase).
			overlay2 = gui.CreateView("ui/text_font.xaml");
			if (overlay2 != null)
				overlay2.SetDataContext(dataContext);

			return true;
		}

		public override bool Shutdown()
		{
			NoesisIntegration gui = NoesisIntegration.Get();
			if (gui != null)
			{
				if (overlay1 != null) { gui.DestroyView(overlay1); overlay1 = null; }
				if (overlay2 != null) { gui.DestroyView(overlay2); overlay2 = null; }
				if (objectGui != null) { gui.DestroyObject(objectGui); objectGui = null; }

				if (dataContext != null)
				{
					dataContext.Changed -= OnDataContextChanged;
					gui.DestroyDataContext(dataContext);
					dataContext = null;
				}
			}
			return true;
		}

		private void OnDataContextChanged(string name)
		{
			if (name == "sun_angle_x")
			{
				RotateSun(dataContext.GetFloat("sun_angle_x"));
			}
			else if (name == "time_preset")
			{
				int preset = dataContext.GetInt("time_preset");
				if (preset >= 0 && preset < TIME_PRESET_ANGLES.Length)
					dataContext.SetFloat("sun_angle_x", TIME_PRESET_ANGLES[preset]);
			}
		}

		private void RotateSun(float angleX)
		{
			Node sun = World.GetNodeByName("sun");
			if (sun != null)
				sun.WorldTransform = new Mat4(MathLib.RotateX(angleX));
		}
	}
}
