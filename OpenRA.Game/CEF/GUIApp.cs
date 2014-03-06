using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUIApp : CefApp
	{
		private GUIRenderProcessHandler renderProcessHandler;
		private GUIBrowserProcessHandler browserProcessHandler;

		public GUIApp()
		{
			renderProcessHandler = new GUIRenderProcessHandler();
			browserProcessHandler = new GUIBrowserProcessHandler();
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
