using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Windows.Media.Imaging;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class SHPHandlerFactory : CefSchemeHandlerFactory
	{

		protected override CefResourceHandler Create(CefBrowser browser, CefFrame frame, string schemeName, CefRequest request)
		{
			return new SHPResourceHandler();
		}
	}

	class SHPResourceHandler : CefResourceHandler
	{
		private byte[] data;
		private string dataType;
		private int written;

		protected override bool ProcessRequest(CefRequest request, CefCallback callback)
		{
			if (request.Url.Contains("image"))
			{
				var bitmap = Graphics.CursorProvider.GetCursorSequence("default").GetSprite(0).sheet.AsBitmap();
				MemoryStream ms = new MemoryStream();
				bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
				data = ms.ToArray();

				dataType = "image/png";
				written = 0;

				callback.Continue();
				
				return true;
			}

			return false;
		}

		protected override void GetResponseHeaders(CefResponse response, out long responseLength, out string redirectUrl)
		{
			response.Status = 200;
			responseLength = data.Length;
			response.MimeType = dataType;
			redirectUrl = null;
		}

		protected override bool ReadResponse(Stream response, int bytesToRead, out int bytesRead, CefCallback callback)
		{
			int writeLength = 0;
			bool ret = true;

			if (bytesToRead + written < data.Length)
			{
				writeLength = bytesToRead;
				callback.Continue();
			}
			else
			{
				writeLength = data.Length - written;
				ret = false;
			}

			response.Write(data, written, writeLength);
			written += writeLength;
			bytesRead = written;
			return ret;
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
			data = null;
			written = 0;
			dataType = "";
		}
	}
}
