// System logic: create the Noesis integration and register fonts + theme once, before any
// world is loaded.

using Unigine;

namespace UnigineApp
{
	class AppSystemLogic : SystemLogic
	{
		private NoesisIntegration noesisGui;

		public override bool Init()
		{
			noesisGui = new NoesisIntegration();
			bool isInited = noesisGui.Init();

			if (isInited)
			{
				noesisGui.SetDefaultFontSize(16.0f);
				noesisGui.SetGlyphCacheSize(2048);

				noesisGui.RegisterFont("ui/fonts/Muli-Regular.ttf");
				noesisGui.RegisterFont("ui/fonts/CourierPrime-Regular.ttf");
				noesisGui.RegisterFont("ui/fonts/Caladea-Regular.ttf");

				noesisGui.RegisterFont("ui/themes/noesis/Fonts/PT Root UI_Regular.otf");
				noesisGui.RegisterFont("ui/themes/noesis/Fonts/PT Root UI_Bold.otf");

				
				noesisGui.SetFontFallbacks("ui/themes/noesis/Fonts/#PT Root UI");
				noesisGui.SetApplicationResources("ui/themes/noesis/NoesisTheme.DarkBlue.xaml");
			}

			return isInited;
		}

		public override bool Shutdown()
		{
			noesisGui?.Shutdown();
			noesisGui = null;
			return true;
		}
	}
}
