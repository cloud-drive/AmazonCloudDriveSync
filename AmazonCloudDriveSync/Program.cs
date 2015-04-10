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
using System.Security.Cryptography;
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
            Console.WriteLine("Press a key to begin");  Console.ReadKey();
            config = new ConfigOperations.ConfigData();
            if (File.Exists(ConfigurationManager.AppSettings["jsonConfig"]))
                config = JsonConvert.DeserializeObject<ConfigOperations.ConfigData>(File.ReadAllText(ConfigurationManager.AppSettings["jsonConfig"]));
            config.updateConfig(() => { File.WriteAllText(ConfigurationManager.AppSettings["jsonConfig"], JsonConvert.SerializeObject(config)); });
            
            Console.WriteLine("We've got a good access token, let's go.");
            Folder rootFolder = new Folder() {cloudId=config.cloudMainFolderId, localDirectory=new DirectoryInfo(ConfigurationManager.AppSettings["localFolder"])};
            WalkDirectoryTree(rootFolder, (s, t) => { Console.WriteLine("{0} in {1}:{2}", s, t.cloudId, t.localDirectory.FullName); updateSingleFile(s, t); }, (s) => { Console.WriteLine(s.localDirectory.FullName); });

            Console.ReadKey();
        }
        private static void updateSingleFile(String localFilename, Folder cloudParent)
        {
            CloudDriveOperations.CloudDriveListResponse<CloudDriveOperations.CloudDriveFile> fileSearch = CloudDriveOperations.getFileByNameAndParentId(config, cloudParent.cloudId, Path.GetFileName(localFilename));
            switch (fileSearch.count)
            {
                case (0):
                    CloudDriveOperations.uploadFile(config, localFilename, cloudParent.cloudId);
                    //create the file
                    break;
                case (1):
                    //update the file if necessary
                    break;
                default:
                    //we have more than one result
                    break;
            }
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
        private static string getMD5hash(string filename)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filename))
                return Encoding.UTF8.GetString( md5.ComputeHash(stream));
        }
        private class Folder
        {
           public String cloudId;
           public DirectoryInfo localDirectory;
        }
    }



}
