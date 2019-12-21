using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOrientedMultiThread.Core
{
    public class CrashDump
    {
        private string rootFolder = "CrashDump";
        private string folderPath;
        public CrashDump(string id)
        {
            folderPath = Path.Combine(rootFolder, id);
        }
        public async Task<Dictionary<string, string>> AnyCrashDump()
        {
            Dictionary<string, string> jsonByDumpName = new Dictionary<string, string>();
            if (Directory.Exists(folderPath))
            {
                jsonByDumpName = await GetJsonByDumpName();
            }
            return jsonByDumpName;
        }
        public async Task CreateDumpFiles(Dictionary<string, string> jsonByVariableName)
        {
            Directory.CreateDirectory(folderPath);
            foreach (var jsonString in jsonByVariableName)
            {
                await Helper.WriteAsync(Path.Combine(folderPath, jsonString.Key + ".json"), jsonString.Value);
            }
        }
        private async Task<Dictionary<string, string>> GetJsonByDumpName()
        {
            var fullfileNames = Directory.GetFiles(folderPath);
            Dictionary<string, string> jsonByDumpName = new Dictionary<string, string>();

            foreach (var fullfileName in fullfileNames)
            {
                FileInfo fileinfo = new FileInfo(fullfileName);
                string variableName = fileinfo.Name.Replace(fileinfo.Extension, "");
                var fileStream = await Helper.ReadAsync(fullfileName);
                string json = Encoding.UTF8.GetString(fileStream, 0, fileStream.Length);
                jsonByDumpName.Add(variableName, json);
                //fileinfo.Delete();
            }
            //Directory.Delete(folderPath);
            return jsonByDumpName;
        }
    }
}
