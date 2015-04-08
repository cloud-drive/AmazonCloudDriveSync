using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        public static void createFolder(ConfigOperations.ConfigData config, string name, string parentId)
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
        public class CloudDriveNode
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
        public class CloudDriveFolder : CloudDriveNode
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
