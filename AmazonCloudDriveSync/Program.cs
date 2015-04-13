using CloudDriveLayer;
using CloudDriveLayer.CloudDriveModels;
using CloudDriveLayer.ConfigOperations;
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
        public static ConfigData config;
        public static SemaphoreSlim threadLock;

        static void Main(string[] args)
        {
            Console.WriteLine("Press a key to begin"); Console.ReadKey();
            createConfig();
            threadLock = new SemaphoreSlim(3);
            if (File.Exists(ConfigurationManager.AppSettings["jsonConfig"]))
                config = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(ConfigurationManager.AppSettings["jsonConfig"]));
            config.updateConfig(() => { File.WriteAllText(ConfigurationManager.AppSettings["jsonConfig"], JsonConvert.SerializeObject(config)); });

            Console.WriteLine("We've got a good access token, let's go.");
            Folder rootFolder = new Folder() {cloudId=config.cloudMainFolderId, localDirectory=new DirectoryInfo(ConfigurationManager.AppSettings["localFolder"])};
            WalkDirectoryTree(rootFolder, 
                (filename, parentFolder) => { 
                    Console.WriteLine("{0} in {1}:{2}", filename, parentFolder.cloudId, parentFolder.localDirectory.FullName);
                    updateSingleFile(filename, parentFolder);
                }, 
                (folderName) => {
                    Console.WriteLine(folderName.localDirectory.FullName); 
                });
            Console.WriteLine("All done!");
            Console.ReadKey();
        }

        private static void updateSingleFile(String localFilename, Folder cloudParent)
        {
            //rule #1 - avoid uploading if we can.  matching md5s mean the file is already in cloud
            config.updateConfig(() => { File.WriteAllText(ConfigurationManager.AppSettings["jsonConfig"], JsonConvert.SerializeObject(config)); });
            CloudDriveListResponse<CloudDriveFile> fileSearch = CloudDriveOperations.getFilesByName(config, Path.GetFileName(localFilename));
            List<CloudDriveFile> fileSearchCleaned = new List<CloudDriveFile>();
            if (fileSearch.count > 0)
                fileSearchCleaned = fileSearch.data.Where(x => x.name == Path.GetFileName(localFilename)).ToList<CloudDriveFile>();
            switch (fileSearchCleaned.Count)
            {
                case (0):
                    CloudDriveOperations.uploadFile(config, localFilename, cloudParent.cloudId, true);
                    //create the file
                    break;
                case (1):
                    bool md5Match = (fileSearchCleaned[0].contentProperties.md5 == getMD5hash(localFilename));
                    bool parentMatch = fileSearchCleaned[0].parents.Contains(cloudParent.cloudId);

                    if (md5Match && !parentMatch)
                        //if md5 matches & parent doesnt match, add parent
                        CloudDriveOperations.addNodeParent(config, fileSearchCleaned[0].id, cloudParent.cloudId);
                    else if (!md5Match && !parentMatch)
                        //this other file is same name but unrelated, upload force
                        // if possible to *copy* the existing cloud node to an additional name, that would be preferable
                        CloudDriveOperations.uploadFile(config, localFilename, cloudParent.cloudId, true);
                    else if (!md5Match && parentMatch)
                        //file has changed, need to update content
                        CloudDriveOperations.uploadFileContent(config, localFilename, fileSearchCleaned[0].id);
                    break;
                default:
                    string localMd5 = getMD5hash(localFilename);
                    //multiple files have the same filename.  look for an Md5 match.
                    var matchingMd5 = fileSearchCleaned.Where(x => x.contentProperties.md5 == localMd5).FirstOrDefault();
                    if (matchingMd5==null)
                        CloudDriveOperations.uploadFile(config, localFilename, cloudParent.cloudId, true);
                    else if (!matchingMd5.parents.Contains(cloudParent.cloudId))
                            CloudDriveOperations.addNodeParent(config, matchingMd5.id, cloudParent.cloudId);
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
                {
                    threadLock.Wait();
                    ThreadPool.QueueUserWorkItem(o =>
                        {
                            fileOperation(fi.FullName, root);
                            threadLock.Release();
                        });
                }

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
            {
                byte[] data = md5.ComputeHash(stream);
                StringBuilder sBuilder = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                    sBuilder.Append(data[i].ToString("x2"));
                return sBuilder.ToString();
            }

        }
        private class Folder
        {
           public String cloudId;
           public DirectoryInfo localDirectory;
        }
        private static void createConfig()
        {
            config = new ConfigData(
            ConfigurationManager.AppSettings["appKey"],
            ConfigurationManager.AppSettings["appSecret"],
            ConfigurationManager.AppSettings["cloudFolder"],
            ConfigurationManager.AppSettings["oauthxRedirect"],
            ConfigurationManager.AppSettings["oauthxBase"]
            );
        }
    }



}
