using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUIBrowserProcessHandler : CefBrowserProcessHandler
	{
		CefWindowInfo windowInfo;
		CefBrowserSettings browserSettings;
		CefBrowser browser;
		public CefBrowser Browser { get { return browser; } }
		CefClient client;

		public GUIBrowserProcessHandler (CefClient client)
		{
			this.client = client;

			windowInfo = CefWindowInfo.Create();
			windowInfo.SetAsOffScreen(IntPtr.Zero);
			windowInfo.SetTransparentPainting(true);

			browserSettings = new CefBrowserSettings();
			browserSettings.FileAccessFromFileUrls = CefState.Enabled;
		}

		public void Shutdown()
		{
			browser.GetHost().CloseBrowser(false);
		}

		protected override void OnContextInitialized()
		{
			CefRuntime.RegisterSchemeHandlerFactory("openra", "shp", new SHPHandlerFactory());

			var location = System.Reflection.Assembly.GetEntryAssembly().Location;
			var directoryPath = Path.GetDirectoryName(location);

			browser = CefBrowserHost.CreateBrowserSync(windowInfo, client, browserSettings);
			browser.GetMainFrame().LoadUrl("file:///" + directoryPath + "/mods/cnc/gui/mainmenu.html");
		}
	}
}
