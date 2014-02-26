﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using OpenRA.FileFormats;

namespace OpenRA.Utility
{
	class YamlToHtml
	{
		public void WriteYamlNode(StreamWriter sw, MiniYamlNode node)
		{
			sw.WriteLine("<div class='node {0}'><div class='key'>{0}</div>", node.Key);

			WriteYaml(sw, node.Value);

			sw.WriteLine("</div>");
		}

		public void WriteYamlNodeList(StreamWriter sw, List<MiniYamlNode> nodes)
		{
			foreach (var node in nodes)
			{
				WriteYamlNode(sw, node);
			}
		}

		public void WriteYaml(StreamWriter sw, MiniYaml yaml)
		{
			sw.WriteLine("<div class='value'>{0}</div>", yaml.Value);

			WriteYamlNodeList(sw, yaml.Nodes);
		}

		public void WriteYamlFile(StreamWriter sw, string file)
		{
			var fileOutput = file.Substring(file.LastIndexOf('\\') + 1);
			fileOutput = fileOutput.Substring(0, fileOutput.IndexOf(".yaml"));
			sw.WriteLine("<div class='file {0}'><div class='value'>{0}</div>", fileOutput);

			List<MiniYamlNode> yamlFile = MiniYaml.FromFile(file);
			WriteYamlNodeList(sw, yamlFile);

			sw.WriteLine("</div>");
		}

		public void ProcessDirectory(string dir, string output, bool recursive = true)
		{
			//sw.WriteLine("<div class='directory {0}'>", dir.Substring(dir.LastIndexOf('\\') + 1));
			Console.WriteLine(output + "\\" + dir.Substring(dir.LastIndexOf("\\") + 1) + ".html");
			using (StreamWriter sw = new StreamWriter(output + "\\" + dir.Substring(dir.LastIndexOf("\\") + 1) + ".html"))
			{
				string[] files = Directory.GetFiles(dir);
				foreach (var file in files.Where(f => f.EndsWith(".yaml")))
				{
					WriteYamlFile(sw, file);
				}
			}
			if (recursive)
			{
				string[] directories = Directory.GetDirectories(dir);
				foreach (var d in directories)
				{
					ProcessDirectory(d, output, recursive);
				}
			}
			//sw.WriteLine("</div>");
		}

		public void Run(string where, string output = "html", bool recursive = true)
		{
			if (!Directory.Exists(output))
				Directory.CreateDirectory(output);

				ProcessDirectory(where, output, recursive);
		}
	}
}
