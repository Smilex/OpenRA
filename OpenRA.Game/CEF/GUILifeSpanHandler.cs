using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUILifeSpanHandler : CefLifeSpanHandler
	{
		protected override bool DoClose(CefBrowser browser)
		{
			browser.GetHost().ParentWindowWillClose();

			return false;
		}
	}
}
