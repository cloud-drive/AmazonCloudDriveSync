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
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace AmazonCloudDriveSync
{
    public class miniFile
    {
        public String id;
        public String name;
        public String md5;
        public List<String> parentIds;
        public miniFile()
        {
            parentIds = new List<string>();
        }
    }
    class Program
    {
        public static ConfigData config;
        public static MemoryCache fileCache;
        public static MemoryCache folderCache;
        public static SemaphoreSlim threadLock;
        public static SemaphoreSlim configLock;
        private static bool cancel = false;
        public static List<miniFile> memFiles;

        static void Main(string[] args)
        {
           // Console.WriteLine("Press a key to begin"); Console.ReadKey();
            config = new ConfigData();
            threadLock = new SemaphoreSlim(10, 10);
            configLock = new SemaphoreSlim(1);
            fileCache = new MemoryCache("AmazonCloudDriveSync_File");
            folderCache = new MemoryCache("AmazonCloudDriveSync_Folder");
            memFiles = new List<miniFile>();
            setConsoleSize();
            var autoResetEvent = new AutoResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                cancel = true;
                eventArgs.Cancel = true;
                Console.WriteLine("**** Will end once current threads complete ****");
            };

            if (File.Exists(ConfigurationManager.AppSettings["jsonConfig"]))
                config = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(ConfigurationManager.AppSettings["jsonConfig"]));
            Console.WriteLine("Please enter a refresh token if you need to:");
            String refreshOption = Console.ReadLine();
            if (refreshOption.Length>0) 
                config.refreshAccessToken(refreshOption, ConfigurationManager.AppSettings["appKey"], ConfigurationManager.AppSettings["appSecret"]);
            updateConfig();
            config.updateConfig(() => { File.WriteAllText(ConfigurationManager.AppSettings["jsonConfig"], JsonConvert.SerializeObject(config)); });

            Console.WriteLine("We've got a good access token, let's go.");
            loadCache2();

            Folder rootFolder = new Folder() {cloudId=config.cloudMainFolderId, localDirectory=new DirectoryInfo(ConfigurationManager.AppSettings["localFolder"])};
            WalkDirectoryTree(rootFolder, 
                (filename, parentFolder) => { 
                    Console.WriteLine("{0} in {1}", filename, parentFolder.cloudId);
                    updateSingleFile(filename, parentFolder);
                }, 
                (folderName) => {
                    Console.WriteLine(folderName.localDirectory.FullName); 
                });

            Console.WriteLine("All done!");
            Console.ReadKey();
        }

        private static void loadCache()
        {
            CloudDriveListResponse<CloudDriveFolder> allFolders = CloudDriveOperations.getFolders(config, "");
            foreach (CloudDriveFolder x in allFolders.data)
                folderCache.Add(x.id, x, DateTime.Now.AddHours(1));
            Console.WriteLine("Folder cache loaded: {0} items", folderCache.GetCount());
            writeCache("folder_cache.json", allFolders.data);
            
            CloudDriveListResponse<CloudDriveFile> allFiles = 
                CloudDriveOperations.getAllFiles(config);
                // JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(ConfigurationManager.AppSettings["jsonConfig"]))
            foreach (CloudDriveFile x in allFiles.data)
                fileCache.Add(x.id, x, DateTime.Now.AddHours(1));
            Console.WriteLine("File cache loaded: {0} items", fileCache.GetCount());
            writeCache("file_cache.json", allFiles.data);

        }
        private static void loadCache2()
        {
            List<CloudDriveFolder> allFolders = JsonConvert.DeserializeObject<List<CloudDriveFolder>>(File.ReadAllText("folder_cache.json"));
                //CloudDriveOperations.getFolders(config, "");
            foreach (CloudDriveFolder x in allFolders)
            {
                folderCache.Add(x.id, x, DateTime.Now.AddHours(1));
                
            }
            Console.WriteLine("Folder cache loaded: {0} items", folderCache.GetCount());
            //writeCache("folder_cache.json", allFolders.data);

            List<CloudDriveFile> allFiles = JsonConvert.DeserializeObject<List<CloudDriveFile>>(File.ReadAllText("file_cache.json"));
            foreach (CloudDriveFile x in allFiles)
            {
                fileCache.Add(x.id, x, DateTime.Now.AddHours(1));
                memFiles.Add(new miniFile() { id = x.id, name = x.name, md5=x.contentProperties.md5, parentIds = x.parents });
            }
            Console.WriteLine("File cache loaded: {0} items", fileCache.GetCount());

        }
        public static void writeCache<T>(String filename, List<T> x)
        {
            using (StreamWriter sw = new StreamWriter(new FileStream(filename, FileMode.Create)))
            using (JsonWriter jw = new JsonTextWriter(sw))
            {
                JsonSerializer ser = new JsonSerializer();
                //jw.WriteStartObject();
                jw.WriteStartArray();
                foreach (Object p in x)
                {
                    JObject obj = JObject.FromObject(p, ser);
                    obj.WriteTo(jw);
                    jw.Flush();
                }
                jw.WriteEndArray();
                //jw.WriteEndObject();
            }
        }
        private static T checkCache<T>(String name, Task<T> getT, MemoryCache fileCache)
        {
            if (!fileCache.Contains(name))
            {
                Console.WriteLine("Cache miss! {0}", name);
                T val = getT.Result;
                fileCache.Add(name, val, DateTime.Now.AddHours(1));
            }
            return (T)fileCache.Get(name);
        }
        private static void updateSingleFile(String localFilename, Folder cloudParent)
        {
            //rule #1 - avoid uploading if we can.  matching md5s mean the file is already in cloud
            Task<String> getHash = Task.Factory.StartNew(() => { return getMD5hash(localFilename); });
            config.updateTokens(() => { configLock.Wait(); File.WriteAllText(ConfigurationManager.AppSettings["jsonConfig"], JsonConvert.SerializeObject(config)); configLock.Release(); });

            CloudDriveFolder actualParent = checkCache<CloudDriveFolder>(cloudParent.cloudId, Task.Factory.StartNew<CloudDriveFolder>(
                () => { return CloudDriveOperations.getFolder(config, cloudParent.cloudId); }
                ), folderCache);
            String localMd5 = getHash.Result;
            if (memFiles.Any(item => item.parentIds.Contains(cloudParent.cloudId) && item.name == Path.GetFileName(localFilename) && item.md5 == localMd5))
                 return;
            CloudDriveListResponse<CloudDriveFile> fileSearch = 
                CloudDriveOperations.getFilesByName(config, Path.GetFileName(localFilename));
            List<CloudDriveFile> fileSearchCleaned = new List<CloudDriveFile>();
            if (fileSearch.count > 0)
                fileSearchCleaned = fileSearch.data.Where(x => x.name == Path.GetFileName(localFilename)).ToList<CloudDriveFile>();
            switch (fileSearchCleaned.Count)
            {
                case (0):
                    Console.WriteLine("Does not exist in cloud, need to upload {0}", localFilename);
                    CloudDriveOperations.uploadFile(config, localFilename, cloudParent.cloudId, true);
                    //create the file
                    break;
                case (1):
                    Console.WriteLine("Exists in cloud, need to compare {0}", localFilename);
                    bool md5Match = (fileSearchCleaned[0].contentProperties.md5 == localMd5);
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
            if (cancel) return;
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
                    if (cancel) return;
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
        private static void updateConfig()
        {
            config.updateValues(
                ConfigurationManager.AppSettings["appKey"],
                ConfigurationManager.AppSettings["appSecret"],
                ConfigurationManager.AppSettings["cloudFolder"],
                ConfigurationManager.AppSettings["oauthxRedirect"],
                ConfigurationManager.AppSettings["oauthxBase"]
                );
        }
        static void setConsoleSize()
        {
            System.Console.SetWindowPosition(0, 0);   // sets window position to upper left
            System.Console.SetBufferSize(200, 20000);   // make sure buffer is bigger than window
            System.Console.SetWindowSize(160, 84);   //set window size to almost full screen 
        }  // End  setConsoleSize()


    }



}
