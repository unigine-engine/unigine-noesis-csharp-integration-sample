// Wrapper around a Noesis view used as a 2D overlay (rect, enabled, data context).

using Unigine;

namespace UnigineApp
{
	public sealed class NoesisView
	{
		private readonly Noesis.View view;
		private readonly string xamlPath;
		private ivec4 rect = new ivec4(0, 0, 0, 0);
		private bool rectFullWindow = true;
		private bool enabled = true;
		private NoesisDataContext dataCtx = null; // non-owning

		public NoesisView(Noesis.View view, string xamlPath)
		{
			this.view = view;
			this.xamlPath = xamlPath;
		}

		public void Shutdown()
		{
			view?.Renderer?.Shutdown();
		}

		public void SetEnabled(bool value) { enabled = value; }
		public bool IsEnabled() { return enabled; }

		// Viewport rect in client pixels. Input is clipped to this rect.
		public void SetRect(int x, int y, int width, int height)
		{
			rect = new ivec4(x, y, width, height);
			rectFullWindow = false;
		}

		public void SetRectFullWindow()
		{
			rectFullWindow = true;
			rect = new ivec4(0, 0, 0, 0);
		}

		public bool IsRectFullWindow() { return rectFullWindow; }
		public ivec4 GetRect() { return rect; } // (x, y, w, h); (0,0,0,0) when full-window

		public string GetXamlPath() { return xamlPath; }

		public Noesis.View GetNoesisView() { return view; }

		public NoesisDataContext GetDataContext() { return dataCtx; }

		public void SetDataContext(NoesisDataContext ctx)
		{
			dataCtx = ctx;
			Noesis.FrameworkElement root = view?.Content;
			if (root != null)
				root.DataContext = ctx;
		}
	}
}
