// Noesis resource providers (font / xaml / texture) backed by Unigine File + Image:
//   * base classes Noesis.FontProvider / XamlProvider / TextureProvider;
//   * providers use System.Uri and return System.IO.Stream;
//   * FontProvider.RegisterFont(Uri folder, string filename) is 2-arg — the base derives
//     family/weight/style from the font file itself;
//   * TextureProvider.LoadTexture(Uri) has no RenderDevice parameter.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unigine;

namespace UnigineApp
{
	internal static class NoesisIo
	{
		// Reads a whole file through the Unigine filesystem (honours mounts), returns null on failure.
		public static byte[] ReadFileBytes(string path)
		{
			File file = new File();
			if (!file.Open(path, "rb"))
				return null;

			ulong size = file.GetSize();
			byte[] bytes = new byte[size];
			if (size > 0)
			{
				IntPtr buffer = Marshal.AllocHGlobal((int)size);
				file.Read(buffer, size);
				Marshal.Copy(buffer, bytes, 0, (int)size);
				Marshal.FreeHGlobal(buffer);
			}
			file.Close();
			return bytes;
		}

		public static string UriPath(Uri uri)
		{
			return uri.OriginalString;
		}
	}

	public sealed class NoesisFontProvider : Noesis.FontProvider
	{
		private readonly string rootPath;
		private readonly Dictionary<string, string> filePaths = new Dictionary<string, string>();
		private readonly Dictionary<string, byte[]> buffers = new Dictionary<string, byte[]>();

		public NoesisFontProvider(string rootPath)
		{
			this.rootPath = rootPath;
		}

		// filePath: font file relative to the data root (e.g. "ui/fonts/Muli-Regular.ttf").
		public void RegisterFace(string filePath)
		{
			int slash = filePath.LastIndexOf('/');
			string folder = slash >= 0 ? filePath.Substring(0, slash) : "./";
			string filename = slash >= 0 ? filePath.Substring(slash + 1) : filePath;

		
			filePaths[filename] = filePath;

			RegisterFont(new Uri(folder, UriKind.RelativeOrAbsolute), filename);
		}

		public override void ScanFolder(Uri folder) { }

		public override System.IO.Stream OpenFont(Uri folder, string filename)
		{
			byte[] bytes;
			if (!buffers.TryGetValue(filename, out bytes))
			{
				string diskPath;
				if (!filePaths.TryGetValue(filename, out diskPath))
				{
					// Unregistered file: resolve relative to the requested folder Uri.
					string folderPath = NoesisIo.UriPath(folder);
					diskPath = string.IsNullOrEmpty(folderPath) ? filename : folderPath + "/" + filename;
				}

				string fullPath = rootPath + "/" + diskPath;
				bytes = NoesisIo.ReadFileBytes(fullPath);
				if (bytes == null)
				{
					Log.Warning("NoesisGUI: failed to open font \"{0}\"\n", fullPath);
					return null;
				}
				buffers[filename] = bytes;
			}
			return new System.IO.MemoryStream(bytes, false);
		}
	}

	public sealed class NoesisXamlProvider : Noesis.XamlProvider
	{
		private readonly string rootPath;
		private readonly Dictionary<string, byte[]> buffers = new Dictionary<string, byte[]>();

		public NoesisXamlProvider(string rootPath)
		{
			this.rootPath = rootPath;
		}

		public override System.IO.Stream LoadXaml(Uri uri)
		{
			string key = NoesisIo.UriPath(uri);

			byte[] bytes;
			if (!buffers.TryGetValue(key, out bytes))
			{
				string path = rootPath + "/" + key;
				bytes = NoesisIo.ReadFileBytes(path);
				if (bytes == null)
				{
					Log.Warning("NoesisGUI: XAML not found \"{0}\"\n", path);
					return null;
				}
				buffers[key] = bytes;
			}
			return new System.IO.MemoryStream(bytes, false);
		}
	}

	public sealed class NoesisTextureProvider : Noesis.TextureProvider
	{
		private readonly string rootPath;

		public NoesisTextureProvider(string rootPath)
		{
			this.rootPath = rootPath;
		}

		// This sample's XAML references no image textures, so this path is not exercised;
		// return an empty info (Rect/DpiScale are get-only on the struct).
		public override Noesis.TextureProvider.TextureInfo GetTextureInfo(Uri uri)
		{
			return new Noesis.TextureProvider.TextureInfo();
		}

		public override Noesis.Texture LoadTexture(Uri uri)
		{
			Image image;
			if (!LoadImageRgba8(uri, out image))
				return null;

			bool hasAlpha = true;
			if (image.Format != Image.FORMAT_RGBA8)
			{
				if (image.NumChannels != 4)
					hasAlpha = false;
				image.ConvertToFormat(Image.FORMAT_RGBA8);
			}

			Texture tex = new Texture();
			tex.Create(image);
			return new NoesisTexture(tex, hasAlpha);
		}

		private bool LoadImageRgba8(Uri uri, out Image outImage)
		{
			outImage = null;
			string path = rootPath + "/" + NoesisIo.UriPath(uri);
			byte[] bytes = NoesisIo.ReadFileBytes(path);
			if (bytes == null)
			{
				Log.Warning("NoesisGUI: texture not found \"{0}\"\n", path);
				return false;
			}
			Image image = new Image();
			if (!image.Load(bytes, bytes.Length))
			{
				Log.Warning("NoesisGUI: failed to decode texture \"{0}\"\n", path);
				return false;
			}
			outImage = image;
			return true;
		}
	}
}
