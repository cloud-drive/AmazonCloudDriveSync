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
        public static ConfigData config;
        static void Main(string[] args)
        {
            config = new ConfigData();
            if (File.Exists(ConfigurationManager.AppSettings["jsonConfig"]))
                config = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(ConfigurationManager.AppSettings["jsonConfig"]));
            updateConfig(() => { File.WriteAllText(ConfigurationManager.AppSettings["jsonConfig"], JsonConvert.SerializeObject(config)); });
            
            Console.WriteLine("We've got a good access token, let's go.");
            WalkDirectoryTree(new DirectoryInfo(ConfigurationManager.AppSettings["localFolder"]), (s) => { Console.WriteLine(s); });

            Console.ReadKey();
        }

        private static void updateConfig(Action saveConfig)
        {
            if (config.lastTokenReceived.AddSeconds(config.lastToken.expires_in) < DateTime.Now)
                if (String.IsNullOrWhiteSpace(config.lastToken.access_token))
                    getBrandNewToken();
                else
                    refreshAccessToken(config.lastToken.refresh_token, ConfigurationManager.AppSettings["appKey"], ConfigurationManager.AppSettings["appSecret"]);
            if (String.IsNullOrWhiteSpace(config.lastToken.access_token) && (config.lastTokenReceived.AddSeconds(config.lastToken.expires_in) < DateTime.Now))
                getBrandNewToken();
            saveConfig();

            if (config.lastMetaDataCheck.AddDays(3) < DateTime.Now)
                getMetaDataUrl();
            if (String.IsNullOrWhiteSpace(config.rootFolderId))
                getRootFolderId();

            if (String.IsNullOrWhiteSpace(config.cloudMainFolderId) || config.cloudMainFolderId == config.rootFolderId)
            {
                var possibleMainFolders = getFoldersByName(ConfigurationManager.AppSettings["cloudFolder"]);
                if (possibleMainFolders.count > 1) throw new NotImplementedException();
                if (possibleMainFolders.count == 0)
                {
                    createFolder(ConfigurationManager.AppSettings["cloudFolder"], config.rootFolderId);
                }
                else if (possibleMainFolders.count == 1)
                    config.cloudMainFolderId = possibleMainFolders.data[0].id;
            }
            saveConfig();

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
        private static void createFolder(string name,string parentId)
        {
            HttpClient reqAccessToken = new HttpClient();

            Dictionary<String, Object> reqParams = new Dictionary<String, Object>();

            reqParams.Add("name", name);
            reqParams.Add("kind", "FOLDER");
            //reqParams.Add("labels", "");
            //reqParams.Add("properties", "");
            var parentList = new List<String>();
            parentList.Add(parentId);
            reqParams.Add("parents", parentList);
            reqAccessToken.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            reqAccessToken.BaseAddress = new Uri(config.metaData.metadataUrl);
            String jsonContent = JsonConvert.SerializeObject(reqParams);
            StringContent requestContent = new StringContent(jsonContent, UTF8Encoding.UTF8, "application/json");
            Task<HttpResponseMessage> responseTask = reqAccessToken.PostAsync("nodes", requestContent);
            HttpResponseMessage response = responseTask.Result;
            String x = response.Content.ReadAsStringAsync().Result;
            dynamic p = JsonConvert.DeserializeObject(x);
            config.cloudMainFolderId = p.id;
        } 
        private static void getRootFolderId()
        {
            CloudDriveFolder x = getFolders("").data[0];
            String newParent = x.parents[0];
            while (String.IsNullOrWhiteSpace(config.rootFolderId))
            {
                CloudDriveFolder y = getFolder(newParent);
                if (y.parents.Count > 0)
                    newParent = y.parents[0];
                else
                    config.rootFolderId = y.id;
            }
        }
        private static void getBrandNewToken()
        {
            String currentURI = String.Empty;
            String newId = Guid.NewGuid().ToString().Replace("{", "").Replace("}", "");
            String loginWithAmazonUrl = String.Format(
                "https://www.amazon.com/ap/oa?client_id={0}&scope=clouddrive%3Aread%20clouddrive%3Awrite&response_type=code&redirect_uri={1}",
                ConfigurationManager.AppSettings["appKey"],
                ConfigurationManager.AppSettings["oauthxRedirect"]);
            String actualUrl = String.Format(
                "{0}?id={1}&authType=loginWithAmazon&authUrl={2}",
                ConfigurationManager.AppSettings["oauthxBase"],
                newId,
                Convert.ToBase64String(Encoding.Unicode.GetBytes(loginWithAmazonUrl))
                );
            Process.Start(actualUrl);
            WebClient waiter = new WebClient();
            OAuthTransaction finishedAuth = new OAuthTransaction();
            Console.WriteLine("Waiting for authorization...");
            do
            {
                Thread.Sleep(1000);
                finishedAuth = JsonConvert.DeserializeObject<OAuthTransaction>(
                    waiter.DownloadString(ConfigurationManager.AppSettings["oauthxBase"] + newId));
            } while (!finishedAuth.authComplete);

            Console.WriteLine("Got Code: {0}", finishedAuth.authCode);

            String accessToken = getAccessToken(finishedAuth.authCode, ConfigurationManager.AppSettings["appKey"], ConfigurationManager.AppSettings["appSecret"], ConfigurationManager.AppSettings["oauthxRedirect"]);
            AuthTokenResponse accessTokenObj = JsonConvert.DeserializeObject<AuthTokenResponse>(accessToken);
            config.lastToken = accessTokenObj;
            config.lastTokenReceived = DateTime.Now;
        }
        private static void getMetaDataUrl()
        {
            HttpClient reqMetaData = new HttpClient();
            reqMetaData.BaseAddress = new Uri("https://drive.amazonaws.com/");
            reqMetaData.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = reqMetaData.GetStringAsync("drive/v1/account/endpoint").Result;
            config.metaData = JsonConvert.DeserializeObject<MetaDataResponse>(mycontent);
            config.lastMetaDataCheck = DateTime.Now;
        }
        private static String getAccessToken(string code, string key, string secret, string redirect)
        {
            HttpClient reqAccessToken = new HttpClient();

            Dictionary<String, String> reqParams = new Dictionary<String, String>();

            reqParams.Add("grant_type", "authorization_code");
            reqParams.Add("code", code);
            reqParams.Add("client_id", key);
            reqParams.Add("client_secret", secret);
            reqParams.Add("redirect_uri", redirect);

            reqAccessToken.BaseAddress = new Uri("https://api.amazon.com/");

            HttpContent content = new FormUrlEncodedContent(reqParams);
            //String mycontent = content.ReadAsStringAsync().Result;
            Task<HttpResponseMessage> responseTask = reqAccessToken.PostAsync("auth/o2/token", content);
            HttpResponseMessage response = responseTask.Result;

            //reqAccessToken.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            return response.Content.ReadAsStringAsync().Result;
            //reqAccessToken.
        }
        private static void refreshAccessToken(string refresh_token, string key, string secret)
        {
            HttpClient reqAccessToken = new HttpClient();

            Dictionary<String, String> reqParams = new Dictionary<String, String>();

            reqParams.Add("grant_type", "refresh_token");
            reqParams.Add("refresh_token", refresh_token);
            reqParams.Add("client_id", key);
            reqParams.Add("client_secret", secret);

            reqAccessToken.BaseAddress = new Uri("https://api.amazon.com/");

            HttpContent content = new FormUrlEncodedContent(reqParams);
            String mycontent = content.ReadAsStringAsync().Result;
            Task<HttpResponseMessage> responseTask = reqAccessToken.PostAsync("auth/o2/token", content);
            HttpResponseMessage response = responseTask.Result;
            config.lastToken = JsonConvert.DeserializeObject<AuthTokenResponse>(response.Content.ReadAsStringAsync().Result);
            config.lastTokenReceived = DateTime.Now;

        }
        public static CloudDriveListResponse<CloudDriveFolder> getFolders(String id)
        {
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.metadataUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = request.GetStringAsync(id.Length>0?"nodes/"+id+"/children?filters=kind:FOLDER":"nodes?filters=kind:FOLDER").Result;
            return JsonConvert.DeserializeObject<CloudDriveListResponse<CloudDriveFolder>>(mycontent);
        }
        public static CloudDriveListResponse<CloudDriveFolder> getFoldersByName(String name)
        {
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.metadataUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = request.GetStringAsync("nodes?filters=kind:FOLDER AND name:"+name).Result;
            return JsonConvert.DeserializeObject<CloudDriveListResponse<CloudDriveFolder>>(mycontent);
        }
        public static CloudDriveFolder getFolder(String id)
        {
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.metadataUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = request.GetStringAsync("nodes/" + id).Result;
            return JsonConvert.DeserializeObject<CloudDriveFolder>(mycontent);
        }
        private static string ToQueryString(NameValueCollection nvc)
        {
            var array = (from key in nvc.AllKeys
                         from value in nvc.GetValues(key)
                         select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value)))
                .ToArray();
            return "?" + string.Join("&", array);
        }
    }
    public class OAuthTransaction
    {
        public Guid? id { get; set; }
        public String authUrl { get; set; }
        public Boolean authComplete { get; set; }
        public String authCode { get; set; }
        public authTypes authType { get; set; }
        public enum authTypes { other, loginWithAmazon, facebook, twitter }
    }
    public class AccessCodeRequest
    {
        public String grant_type { get; set; }
        public String code { get; set; }
        public String client_id { get; set; }
        public String client_secret { get; set; }
        public String redirect_uri { get; set; }
    }
    public class AuthTokenResponse
    {
        public String token_type { get; set; }
        public Int32 expires_in { get; set; }
        public String refresh_token { get; set; }
        public String access_token { get; set; }
    }
    public class MetaDataResponse
    {
        public Boolean customerExists { get; set; }
        public String contentUrl { get; set; }
        public String metadataUrl { get; set; }
    }
    public class ConfigData
    {
        public AuthTokenResponse lastToken { get; set; }
        public DateTime lastTokenReceived { get; set; }
        public String cloudDriveLocalDirectory { get; set; }
        public String rootFolderId { get; set; }
        public String cloudMainFolderId { get; set; }
        public MetaDataResponse metaData { get; set; }
        public DateTime lastMetaDataCheck { get; set; }
        public ConfigData()
        {
            lastToken = new AuthTokenResponse();
            metaData = new MetaDataResponse();
        }
    }
    class CloudDriveNode
    {
        public string id;
        public string name;
        public string kind;
        public List<string> parents;
        public string createdBy;

        public CloudDriveNode()
        {
            parents = new List<string>();
        }
    }
    class CloudDriveFolder: CloudDriveNode
    {

    }
    public class CloudDriveListResponse<T>
    {
        public Int32 count;
        public String nextToken;
        public List<T> data;
    }
}
