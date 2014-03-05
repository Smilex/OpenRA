using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUIRenderProcessHandler : CefRenderProcessHandler
	{
		private GUIV8Handler v8Handler = new GUIV8Handler();
		protected override void OnContextCreated(CefBrowser browser,CefFrame frame,CefV8Context context)
		{
			CefV8Value windowObj = context.GetGlobal();
			CefV8Value openraObj = CefV8Value.CreateObject(null);
			openraObj.SetValue("version", CefV8Value.CreateString(Game.modData.Manifest.Mod.Version), CefV8PropertyAttribute.ReadOnly);
			windowObj.SetValue("shutdown", CefV8Value.CreateFunction("shutdown", v8Handler), CefV8PropertyAttribute.None);
			windowObj.SetValue("openra", openraObj, CefV8PropertyAttribute.None);
			
		}
	}
}
