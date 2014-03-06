﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUIRenderProcessHandler : CefRenderProcessHandler
	{
		protected override void OnContextCreated(CefBrowser browser,CefFrame frame,CefV8Context context)
		{
			GUIV8Handler v8Handler = new GUIV8Handler();
			CefV8Value windowObj = context.GetGlobal();
			CefV8Value openraObj = CefV8Value.CreateObject(null);
			CefV8Value modObj = CefV8Value.CreateObject(null);

			openraObj.SetValue("version", CefV8Value.CreateString(Game.modData.Manifest.Mod.Version), CefV8PropertyAttribute.ReadOnly);
			openraObj.SetValue("Shutdown", CefV8Value.CreateFunction("Shutdown", v8Handler), CefV8PropertyAttribute.None);
			openraObj.SetValue("GetSequence", CefV8Value.CreateFunction("GetSequence", v8Handler), CefV8PropertyAttribute.None);

			modObj.SetValue("GUI", CefV8Value.CreateString(Game.modData.Manifest.Mod.Id + "/gui/"), CefV8PropertyAttribute.ReadOnly);
			modObj.SetValue("CSS", CefV8Value.CreateString(Game.modData.Manifest.Mod.Id + "/css/"), CefV8PropertyAttribute.ReadOnly);

			openraObj.SetValue("Mod", modObj, CefV8PropertyAttribute.None);
			windowObj.SetValue("OpenRA", openraObj, CefV8PropertyAttribute.None);
			
		}
	}
}
