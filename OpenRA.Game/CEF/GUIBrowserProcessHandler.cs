using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUIBrowserProcessHandler : CefBrowserProcessHandler
	{
		protected override void OnContextInitialized()
		{
			CefRuntime.RegisterSchemeHandlerFactory("openra", "shp", new SHPHandlerFactory());
		}
	}
}
