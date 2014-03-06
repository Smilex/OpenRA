using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	public class GUIApp : CefApp
	{
		private GUIRenderProcessHandler renderProcessHandler;
		private GUIBrowserProcessHandler browserProcessHandler;
		private GUIClient client;
		private CefBrowser Browser { get { return browserProcessHandler.Browser;  } }

		public GUIApp(int width, int height)
		{
			client = new GUIClient(width, height);
			renderProcessHandler = new GUIRenderProcessHandler();
			browserProcessHandler = new GUIBrowserProcessHandler(client);
		}

		public void Initialize()
		{
			CefRuntime.Load();

			var cefMainArgs = new CefMainArgs(new string[0]);

			if (CefRuntime.ExecuteProcess(cefMainArgs, this) != -1)
			{
				Console.Error.WriteLine("CefRuntime could not start the secondary process.");
			}

			var settings = new CefSettings
			{
				SingleProcess = true,
				PackLoadingDisabled = true
			};

			CefRuntime.Initialize(cefMainArgs, settings, this);

			Game.OnQuit += Shutdown;
		}

		public void Shutdown()
		{
			browserProcessHandler.Shutdown();
		}

		public void Render()
		{
			client.Render();
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
				Browser.GetHost().SendMouseClickEvent(cefEvent, type, false, 1);
			}
			else if (input.Event == MouseInputEvent.Up)
			{
				Browser.GetHost().SendMouseClickEvent(cefEvent, type, true, 1);
			}
			else if (input.Event == MouseInputEvent.Move)
			{
				Browser.GetHost().SendMouseMoveEvent(cefEvent, false);
			}
		}

		protected override void OnRegisterCustomSchemes (CefSchemeRegistrar registrar)
		{
			registrar.AddCustomScheme("openra", true, true, false);
		}

		protected override CefRenderProcessHandler GetRenderProcessHandler()
		{
			return renderProcessHandler;
		}

		protected override CefBrowserProcessHandler GetBrowserProcessHandler()
		{
			return browserProcessHandler;
		}
	}
}
