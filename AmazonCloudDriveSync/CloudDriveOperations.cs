using JPT;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmazonCloudDriveSync
{
    public static class CloudDriveOperations
    {

        public static CloudDriveListResponse<CloudDriveFolder> getFolders(ConfigOperations.ConfigData config, String id)
        {
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.metadataUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = request.GetStringAsync(id.Length > 0 ? "nodes/" + id + "/children?filters=kind:FOLDER" : "nodes?filters=kind:FOLDER").Result;
            return JsonConvert.DeserializeObject<CloudDriveListResponse<CloudDriveFolder>>(mycontent);
        }
        public static CloudDriveListResponse<CloudDriveFolder> getChildFolderByName(ConfigOperations.ConfigData config, String parentId, String name)
        {
            if (String.IsNullOrWhiteSpace(parentId) || String.IsNullOrWhiteSpace(name)) return new CloudDriveListResponse<CloudDriveFolder>();
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.metadataUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = request.GetStringAsync("nodes/" + parentId + "/children?filters=kind:FOLDER AND name:" + name).Result;
            return JsonConvert.DeserializeObject<CloudDriveListResponse<CloudDriveFolder>>(mycontent);
        }
        public static CloudDriveListResponse<CloudDriveFolder> getFoldersByName(ConfigOperations.ConfigData config, String name)
        {
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.metadataUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = request.GetStringAsync("nodes?filters=kind:FOLDER AND name:" + name).Result;
            return JsonConvert.DeserializeObject<CloudDriveListResponse<CloudDriveFolder>>(mycontent);
        }


        public static CloudDriveFolder getFolder(ConfigOperations.ConfigData config, String id)
        {
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.metadataUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = request.GetStringAsync("nodes/" + id).Result;
            return JsonConvert.DeserializeObject<CloudDriveFolder>(mycontent);
        }
        public static CloudDriveListResponse<CloudDriveFile> getFileByNameAndParentId(ConfigOperations.ConfigData config, String parentId, String name)
        {
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.metadataUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = request.GetStringAsync("nodes/" + parentId + "/children?filters=kind:FILE AND name:" + name).Result;
            return JsonConvert.DeserializeObject<CloudDriveListResponse<CloudDriveFile>>(mycontent);
        }
        public static CloudDriveListResponse<CloudDriveFile> getFileByNameAndMd5(ConfigOperations.ConfigData config, String name, String md5)
        {
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.metadataUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = request.GetStringAsync("nodes?filters=kind:FILE AND name:'"+ name +"' AND contentProperties.md5:"+md5).Result;
            return JsonConvert.DeserializeObject<CloudDriveListResponse<CloudDriveFile>>(mycontent);
        }
        public static CloudDriveFile getFileById(ConfigOperations.ConfigData config, String id)
        {
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.metadataUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            String mycontent = request.GetStringAsync("nodes/" + id).Result;
            return JsonConvert.DeserializeObject<CloudDriveFile>(mycontent);
        }
        public static String uploadFile(ConfigOperations.ConfigData config, string fullFilePath, string parentId)
        {
            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.contentUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);

            var parentList = new List<String>();
            parentList.Add(parentId);

            Dictionary<string, Object> addNode = new Dictionary<string, Object>() { { "name", Path.GetFileName(fullFilePath) }, { "kind", "FILE" }, {"parents",parentList} };
            String myMetaData = JsonConvert.SerializeObject(addNode, Newtonsoft.Json.Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, });
            using (FileStream file = File.Open(fullFilePath, FileMode.Open, FileAccess.Read))
            {
                MultipartFormDataContent form = new MultipartFormDataContent();
                form.Add(new StringContent(myMetaData), "metadata");

                Download myDownload = new Download();
                var fileStreamContent = new ProgressableStreamContent(file, 8096, myDownload);
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue(MimeTypeMap.MimeTypeMap.GetMimeType(Path.GetExtension(fullFilePath)));
                form.Add(fileStreamContent, "content", Path.GetFileName(fullFilePath));

                var postAsync = request.PostAsync("nodes", form);
                //postAsync.Start();
                TextProgressBar myBar = new TextProgressBar(file.Length,(-1),true);

                while (!postAsync.IsCompleted)
                {
                    myBar.Update(myDownload.Uploaded);
                    Thread.Sleep(1000);
                }
                myBar.Update(myDownload.Uploaded);
                HttpResponseMessage result = postAsync.Result;
                if (result.StatusCode == HttpStatusCode.Conflict)
                {
                    String errorMessage = result.Content.ReadAsStringAsync().Result;

                    return String.Empty;
                }
                if (result.StatusCode == HttpStatusCode.Created)
                    return JsonConvert.DeserializeObject<CloudDriveNode>(result.Content.ReadAsStringAsync().Result).id;
                return String.Empty;
            }
        }

        public static String createFolder(ConfigOperations.ConfigData config, string name, string parentId)
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
            return p.id;
        }
        public class CloudDriveNodeRequest
        {            
            public string name;
            public string kind;
            public List<string> parents;
            public List<string> labels;
            public List<KeyValuePair<string, string>> properties;
            public string createdBy;

            public CloudDriveNodeRequest()
            {
                parents = new List<string>();
                labels = new List<string>();
                properties = new List<KeyValuePair<string, string>>();
            }
        }
        public class ContentProperties
        {
            public UInt64 size;
            public int version;
            public String contentType;
            public string extension;
            public string md5;
        }
        public class CloudDriveNode :CloudDriveNodeRequest
        {
            public string id;
            public string version;
            public DateTime modifiedDate;
            public DateTime createdDate;
            public string status;
            public ContentProperties contentProperties;

            public CloudDriveNode()
            {
                contentProperties = new ContentProperties();
            }
        }
        public class CloudDriveFolder : CloudDriveNode
        {

        }
        public class CloudDriveFile : CloudDriveNode
        {

        }
        public class CloudDriveListResponse<T>
        {
            public Int32 count;
            public String nextToken;
            public List<T> data;
        }


    }
}
