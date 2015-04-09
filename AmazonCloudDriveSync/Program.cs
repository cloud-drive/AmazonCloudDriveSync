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
            Folder rootFolder = new Folder() {cloudId=config.cloudMainFolderId, localDirectory=new DirectoryInfo(ConfigurationManager.AppSettings["localFolder"])};
            WalkDirectoryTree(rootFolder, (s, t) => { Console.WriteLine("{0} in {1}:{2}", s, t.cloudId, t.localDirectory.FullName); }, (s) => { makeSureCloudFolderExists(s); Console.WriteLine(s.localDirectory.FullName); });

            Console.ReadKey();
        }
        private void updateSingleFile(String localFilename, String cloudParent)
        {
            //check for existing file
            //if exists in cloud, compare to cloud file.  if same, return
            //update cloud file
        }
        private static void makeSureCloudFolderExists(Folder myFolder)
        {
            //check for existing folder
            //if exists in cloud return
            //create cloud folder
        }
        private static void WalkDirectoryTree(Folder root, Action<String, Folder> fileOperation, Action<Folder> folderOperation)
        {
            folderOperation(root);
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] subDirs = null;
            try
            {
                files = root.localDirectory.GetFiles("*.*");
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
                    fileOperation(fi.FullName, root);

            subDirs = root.localDirectory.GetDirectories();
            foreach (System.IO.DirectoryInfo dirInfo in subDirs)
                WalkDirectoryTree(new Folder() { cloudId = getOrCreateCloudSubFolderId(root.cloudId, dirInfo.Name), localDirectory = dirInfo}, fileOperation, folderOperation);

        }

        private static string getOrCreateCloudSubFolderId(string parentId, string childName)
        {
            var folderSearch = CloudDriveOperations.getChildFolderByName(config, parentId, childName).data;
            if (folderSearch == null || folderSearch.Count==0)
                return CloudDriveOperations.createFolder(config, childName, parentId);
            if (folderSearch.Count > 0)
                return folderSearch.First().id;
            return String.Empty;
        }
        private class Folder
        {
           public String cloudId;
           public DirectoryInfo localDirectory;
        }
    }



}
