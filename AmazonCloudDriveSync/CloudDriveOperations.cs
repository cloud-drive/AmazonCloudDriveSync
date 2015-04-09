using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
            String mycontent = request.GetStringAsync("nodes/" + parentId + "/children?filters=kind:FILE").Result;
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
            JsonSerializer _jsonWriter = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            HttpClient request = new HttpClient();
            request.BaseAddress = new Uri(config.metaData.contentUrl);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            MultipartFormDataContent form = new MultipartFormDataContent();
            var parentList = new List<String>();
            parentList.Add(parentId);
            List<KeyValuePair<string, string>> newFileProperties = new List<KeyValuePair<string, string>>();
            newFileProperties.Add(new KeyValuePair<string, string>("cloudDriveSyncMD5", "xxx123"));
            //CloudDriveNodeRequest addNode = new CloudDriveNodeRequest() { name = Path.GetFileName(fullFilePath), parents = parentList, kind = "FILE", properties = newFileProperties };
            //FormUrlEncodedContent x = new FormUrlEncodedContent(new Dictionary<string,string>() {{"name",fullFilePath},{ "parents",JsonConvert.SerializeObject(parentList)}, {"kind","FILE"}, {"properties",JsonConvert.SerializeObject(newFileProperties)}});
            Dictionary<string, Object> addNode = new Dictionary<string, Object>() { { "name", Path.GetFileName(fullFilePath) }, { "kind", "FILE" } };
            using (FileStream file = File.Open(fullFilePath, FileMode.Open, FileAccess.Read))
            {
                form.Add(new StringContent(JsonConvert.SerializeObject(addNode, Newtonsoft.Json.Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, })), "metadata");
                //form.Add(x, "metadata");
                form.Add(new StreamContent(file), "content");
                var result = request.PostAsync("nodes?localId=testfile1", form).Result;
                return result.ToString();
            }
            
            

            //dynamic t = JsonConvert.DeserializeObject(result);
            

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
            //public List<string> labels;
            public List<KeyValuePair<string, string>> properties;
            //public string createdBy;

            public CloudDriveNodeRequest()
            {
                parents = new List<string>();
                //labels = new List<string>();
                properties = new List<KeyValuePair<string, string>>();
            }
        }
        public class CloudDriveNode :CloudDriveNodeRequest
        {
            public string id;

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
