using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUIClient : CefClient
	{
		private GUIRenderHandler renderHandler;
		private GUILoadHandler loadHandler;
		private GUIRequestHandler requestHandler;
		private CefSettings settings;

		public GUIClient(int width, int height)
		{
			renderHandler = new GUIRenderHandler(width, height);
			loadHandler = new GUILoadHandler();
			requestHandler = new GUIRequestHandler();
		}

		public void Render()
		{
			renderHandler.Render();
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
