using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUIV8Handler : CefV8Handler
	{
		protected override bool Execute(string name, CefV8Value obj, CefV8Value[] arguments, out CefV8Value returnValue, out string exception)
		{
			if (name == "shutdown")
			{
				returnValue = CefV8Value.CreateUndefined();
				exception = null;
				Game.Exit();
				return true;
			}

			returnValue = null;
			exception = null;
			return false;
		}
	}
}
