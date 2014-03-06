using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using Xilium.CefGlue;

namespace OpenRA.CEF
{
	class GUIV8Handler : CefV8Handler
	{
		protected override bool Execute(string name, CefV8Value obj, CefV8Value[] arguments, out CefV8Value returnValue, out string exception)
		{
			if (name == "Shutdown")
			{
				returnValue = CefV8Value.CreateUndefined();
				exception = null;
				Game.Exit();
				return true;
			}
			else if (name == "GetSequence")
			{
				if (arguments.Length >= 2 && arguments[0].IsString && arguments[1].IsString)
				{
					int frame = 0;

					if (arguments.Length >= 3 && arguments[2].IsInt)
						frame = arguments[2].GetIntValue();

					Graphics.Sequence sequence = Graphics.SequenceProvider.GetSequence(arguments[0].GetStringValue(), arguments[1].GetStringValue());
					Graphics.Sprite sprite = sequence.GetSprite(frame);

					MemoryStream stream = new MemoryStream();
					System.Drawing.Bitmap img = sprite.sheet.AsBitmap(sprite.channel, Game.worldRenderer.Palette("chrome").Palette, sprite.bounds);
					img.Save(stream, ImageFormat.Bmp);

					string dataStr = "data:image/bmp;base64," + Convert.ToBase64String(stream.ToArray());
					returnValue = CefV8Value.CreateString(dataStr);
					exception = null;
					return true;
				}
				else
				{
					exception = "Wrong arguments";
					returnValue = CefV8Value.CreateUndefined();
					return false;
				}
			}

			returnValue = CefV8Value.CreateUndefined();
			exception = null;
			return false;
		}
	}
}
