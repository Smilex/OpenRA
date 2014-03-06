using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	public class GUIClient : CefClient
	{
		private GUIRenderHandler renderHandler;
		private GUILoadHandler loadHandler;
		private GUIRequestHandler requestHandler;
		private CefSettings settings;
		private CefBrowserSettings browserSettings;
		private CefWindowInfo windowInfo;
		private GUIApp app;
		private CefBrowser browser;

		public GUIClient(int width, int height)
		{
			renderHandler = new GUIRenderHandler(width, height);
			loadHandler = new GUILoadHandler();
			requestHandler = new GUIRequestHandler();

			settings = new CefSettings
			{
				SingleProcess = true,
				PackLoadingDisabled = true
			};

			windowInfo = CefWindowInfo.Create();
			windowInfo.SetAsOffScreen(IntPtr.Zero);
			windowInfo.SetTransparentPainting(true);

			browserSettings = new CefBrowserSettings();
			browserSettings.FileAccessFromFileUrls = CefState.Enabled;
			app = new GUIApp();
		}

		~GUIClient()
		{
			CefRuntime.Shutdown();
		}

		public void Initialize()
		{
			CefRuntime.Load();

			var cefMainArgs = new CefMainArgs(new string[0]);

			if (CefRuntime.ExecuteProcess(cefMainArgs, app) != -1)
			{
				Console.Error.WriteLine("CefRuntime could not start the secondary process.");
			}

			CefRuntime.Initialize(cefMainArgs, settings, app);

			var location = System.Reflection.Assembly.GetEntryAssembly().Location;
			var directoryPath = Path.GetDirectoryName(location);

			browser = CefBrowserHost.CreateBrowserSync(windowInfo, this, browserSettings);
			browser.GetMainFrame().LoadUrl("file:///" + directoryPath + "/mods/cnc/gui/mainmenu.html");

			Game.OnQuit += Shutdown;
		}

		public void Shutdown()
		{
			browser.GetHost().CloseBrowser();
		}

		public void Render ()
		{
			renderHandler.Render();
		}

		public void Tick()
		{
			CefRuntime.DoMessageLoopWork();
		}

		public void HandleMouseInput(MouseInput input)
		{

			CefMouseEvent cefEvent = new CefMouseEvent(input.Location.X, input.Location.Y, CefEventFlags.None);
			CefMouseButtonType type = CefMouseButtonType.Left;
			if (input.Button == MouseButton.Left)
			{
				type = CefMouseButtonType.Left;
			}
			else if (input.Button == MouseButton.Middle)
			{
				type = CefMouseButtonType.Middle;
			}
			else if (input.Button == MouseButton.Right)
			{
				type = CefMouseButtonType.Right;
			}

			if (input.Event == MouseInputEvent.Down)
			{
				browser.GetHost().SendMouseClickEvent(cefEvent, type, false, 1);
			}
			else if (input.Event == MouseInputEvent.Up)
			{
				browser.GetHost().SendMouseClickEvent(cefEvent, type, true, 1);
			}
			else if (input.Event == MouseInputEvent.Move)
			{
				browser.GetHost().SendMouseMoveEvent(cefEvent, false);
			}
		}

		protected override CefRenderHandler GetRenderHandler()
		{
			return renderHandler;
		}

		protected override CefLoadHandler GetLoadHandler()
		{
			return loadHandler;
		}

		protected override CefRequestHandler GetRequestHandler()
		{
			return base.GetRequestHandler();
		}

		internal class GUIRequestHandler : CefRequestHandler
		{
			SHPResourceHandler resHandler;
			public GUIRequestHandler()
			{
				resHandler = new SHPResourceHandler();
			}
			protected override CefResourceHandler GetResourceHandler(CefBrowser browser, CefFrame frame, CefRequest request)
			{
				return resHandler;
			}
		}
	}
}
