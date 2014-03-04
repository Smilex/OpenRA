using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUIResourceHandler : CefResourceHandler
	{
		private string data;

		protected override bool ProcessRequest(CefRequest request, CefCallback callback)
		{
			if (request.Url.Contains("openra://"))
			{
				var location = System.Reflection.Assembly.GetEntryAssembly().Location;
				var directoryPath = Path.GetDirectoryName(location);

				request.Url = request.Url.Remove(0, 9);
				request.Url = "file:///" + directoryPath + '/' + request.Url;

				callback.Continue();
				return true;
			}

			return false;
		}

		protected override void GetResponseHeaders(CefResponse response, out long responseLength, out string redirectUrl)
		{
			response.Status = 200;
			responseLength = 0;
			redirectUrl = "";
		}

		protected override bool ReadResponse(Stream response, int bytesToRead, out int bytesRead, CefCallback callback)
		{
			bytesRead = 0;
			return false;
		}

		protected override bool CanGetCookie(CefCookie cookie)
		{
			return false;
		}

		protected override bool CanSetCookie(CefCookie cookie)
		{
			return false;
		}

		protected override void Cancel()
		{
			
		}
	}
}
