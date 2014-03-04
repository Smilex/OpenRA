using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUIApp : CefApp
	{
		protected override void OnRegisterCustomSchemes (CefSchemeRegistrar registrar)
		{
			registrar.AddCustomScheme("openra", true, true, false);
		}
	}
}
