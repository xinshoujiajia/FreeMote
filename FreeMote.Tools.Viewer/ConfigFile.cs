using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static System.Net.Mime.MediaTypeNames;

namespace FreeMote.Tools.Viewer
{
    public class ConfigJson
    {
        public List<string> FileNames { get; set; }
        public uint Top { get; set; }
        public uint Left { get; set; }
        public uint Width { get; set; } = 1280;
        public uint Height { get; set; } = 720;
        public float Scale { get; set; } = 1.0f;
    }
    public class ConfigFile
    {
        public static ConfigJson Load(string filename)
        {
            var text =File.ReadAllText(filename);
            var json = JsonConvert.DeserializeObject<ConfigJson>(text);
            return json;
        }
        public static void Save(string filename, ConfigJson json)
        {
            var text = JsonConvert.SerializeObject(json,Formatting.Indented);
            File.WriteAllText(filename, text);
        }
    }
}
