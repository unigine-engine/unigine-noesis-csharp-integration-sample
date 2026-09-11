// Entry point for the NoesisGUI C# sample:
// Engine.Init -> Engine.Main(system, world) -> Engine.Shutdown.

using System;

using Unigine;

namespace UnigineApp
{
	class UnigineApp
	{
		[STAThread]
		static void Main(string[] args)
		{
			Engine.InitParameters initParams = new Engine.InitParameters();
			initParams.window_title = "UNIGINE Engine: NoesisGUI sample";
			Engine.Init(initParams, args);

			// Keep rendering while the window is in the background.
			Engine.BackgroundUpdate = Engine.BACKGROUND_UPDATE.BACKGROUND_UPDATE_RENDER_NON_MINIMIZED;

			AppSystemLogic systemLogic = new AppSystemLogic();
			AppWorldLogic worldLogic = new AppWorldLogic();

			Engine.Main(systemLogic, worldLogic);

			Engine.Shutdown();
		}
	}
}
