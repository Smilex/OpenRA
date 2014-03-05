using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue;
using OpenRA.FileFormats.Graphics;
using OpenRA.Graphics;
using System.Drawing;
using System.Drawing.Imaging;

namespace OpenRA.CEF
{
	class GUIRenderHandler : CefRenderHandler
	{
		private int width;
		private int height;

		private ITexture texture;
		private IVertexBuffer<Vertex> vertices;
		private Vertex[] verts;
		private IShader shader;

		private Sprite sprite;
		private Sheet sheet;

		public GUIRenderHandler(int width, int height)
		{
			this.width = width;
			this.height = height;
			texture = Game.Renderer.Device.CreateTexture();

			verts = new Vertex[4];
			verts[0] = new Vertex(new float2(0,0), new float2(0,0), new float2(0,0));
			verts[1] = new Vertex(new float2(1, 0), new float2(1, 0), new float2(1, 0));
			verts[2] = new Vertex(new float2(1, 1), new float2(1, 1), new float2(1, 1));
			verts[3] = new Vertex(new float2(0, 1), new float2(0, 1), new float2(0, 1));

			vertices = Game.Renderer.Device.CreateVertexBuffer(4);
			vertices.SetData(verts, 4);

			shader = Game.Renderer.Device.CreateShader("rgba");
		}

		public void Render()
		{
			if (sprite == null) return;

			Game.Renderer.RgbaSpriteRenderer.DrawSprite(sprite, new float2(0,0));
		}

		protected override bool GetScreenInfo(CefBrowser browser, CefScreenInfo screenInfo)
		{
			return false;
		}

		protected override void OnPopupSize(CefBrowser browser, CefRectangle rect)
		{
		}

		protected override void OnPaint(CefBrowser browser, CefPaintElementType type, CefRectangle[] dirtyRects, IntPtr buffer, int width, int height)
		{
			texture.SetData(buffer, width, height);

			if (sprite == null)
			{
				sheet = new Sheet(texture);
				sprite = new Sprite(sheet, new Rectangle(0, 0, this.width, this.height), TextureChannel.Alpha);
			}
		}

		protected override void OnCursorChange(CefBrowser browser, IntPtr cursorHandle)
		{
		}

		protected override void OnScrollOffsetChanged(CefBrowser browser)
		{
		}

		protected override bool GetRootScreenRect(CefBrowser browser, ref CefRectangle rect)
		{
			return GetViewRect(browser, ref rect);
		}

		protected override bool GetScreenPoint(CefBrowser browser, int viewX, int viewY, ref int screenX, ref int screenY)
		{
			screenX = viewX;
			screenY = viewY;
			return true;
		}

		protected override bool GetViewRect(CefBrowser browser, ref CefRectangle rect)
		{
			rect.X = 0;
			rect.Y = 0;
			if (Exts.IsPowerOf2(width))
				rect.Width = width;
			else
				rect.Width = Exts.NextPowerOf2(width);
			if (Exts.IsPowerOf2(height))
				rect.Height = height;
			else
				rect.Height = Exts.NextPowerOf2(height);
			return true;
		}
	}
}
