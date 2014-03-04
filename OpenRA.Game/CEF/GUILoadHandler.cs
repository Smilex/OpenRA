using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUILoadHandler : CefLoadHandler
	{

		public GUILoadHandler()
		{
		}

		protected override void OnLoadStart(CefBrowser browser, CefFrame frame)
		{
			// A single CefBrowser instance can handle multiple requests
			//   for a single URL if there are frames (i.e. <FRAME>, <IFRAME>).
			if (frame.IsMain)
			{
				Console.WriteLine("START: {0}", browser.GetMainFrame().Url);
			}
		}

		protected override void OnLoadEnd(CefBrowser browser, CefFrame frame, int httpStatusCode)
		{
			if (frame.IsMain)
			{
				Console.WriteLine("END: {0}, {1}", browser.GetMainFrame().Url, httpStatusCode);
			}
		}
	}
}
