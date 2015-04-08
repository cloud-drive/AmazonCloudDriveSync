using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace AmazonCloudDriveSync
{
    class Program
    {
        public static ConfigOperations.ConfigData config;
        static void Main(string[] args)
        {
            config = new ConfigOperations.ConfigData();
            if (File.Exists(ConfigurationManager.AppSettings["jsonConfig"]))
                config = JsonConvert.DeserializeObject<ConfigOperations.ConfigData>(File.ReadAllText(ConfigurationManager.AppSettings["jsonConfig"]));
            config.updateConfig(() => { File.WriteAllText(ConfigurationManager.AppSettings["jsonConfig"], JsonConvert.SerializeObject(config)); });
            
            Console.WriteLine("We've got a good access token, let's go.");
            WalkDirectoryTree(new DirectoryInfo(ConfigurationManager.AppSettings["localFolder"]), (s) => { Console.WriteLine(s); });

            Console.ReadKey();
        }
        private static void WalkDirectoryTree(DirectoryInfo root, Action<String> fileOperation)
        {
            Console.WriteLine(root.Name);
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] subDirs = null;
            try
            {
                files = root.GetFiles("*.*");
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (System.IO.DirectoryNotFoundException e)
            {
                Console.WriteLine(e.Message);
            }

            if (files != null)
                foreach (System.IO.FileInfo fi in files)
                    fileOperation(fi.FullName);

            subDirs = root.GetDirectories();
            foreach (System.IO.DirectoryInfo dirInfo in subDirs)
                WalkDirectoryTree(dirInfo, fileOperation);

        }
        /*private static string ToQueryString(NameValueCollection nvc)
        {
            var array = (from key in nvc.AllKeys
                         from value in nvc.GetValues(key)
                         select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value)))
                .ToArray();
            return "?" + string.Join("&", array);
        }*/
    }



}
