using CloudDriveLayer.CloudDriveModels;
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


namespace CloudDriveLayer
{
    public static class CloudDriveOperations
    {
        public class RetryHandler : DelegatingHandler
        {
            private const int MaxRetries = 3;
            public RetryHandler(HttpMessageHandler innerHandler)
                : base(innerHandler)
            { }
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                HttpResponseMessage response = new HttpResponseMessage();
                for (int i = 0; i < MaxRetries; i++)
                {
                    response = await base.SendAsync(request, cancellationToken);
                    if (response.IsSuccessStatusCode)
                        return response;
                    else
                        Console.WriteLine("needing to retry");
                }
                return response;
            }
        }
        public static CloudDriveListResponse<T> listSearch<T>(ConfigOperations.ConfigData config, String command)
        {
            HttpClient request = createAuthenticatedClient(config, config.metaData.metadataUrl);

                String mycontent = request.GetStringAsync(command).Result;

            return JsonConvert.DeserializeObject<CloudDriveListResponse<T>>(mycontent);
        }
        public static T nodeSearch<T>(ConfigOperations.ConfigData config, String command)
        {
            HttpClient request = createAuthenticatedClient(config, config.metaData.metadataUrl);
            String mycontent = request.GetStringAsync(command).Result;
            return JsonConvert.DeserializeObject<T>(mycontent);
        }
        public static T nodeChange<T>(ConfigOperations.ConfigData config, String command, HttpContent body)
        {
            HttpClient request = createAuthenticatedClient(config, config.metaData.metadataUrl);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var mycontent = request.PutAsync(command, body).Result;
            var result = mycontent.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<T>(result);
        }
        public static HttpClient createAuthenticatedClient(ConfigOperations.ConfigData config, String url)
        {
            HttpClient request = new HttpClient(new RetryHandler(new HttpClientHandler()));
            request.BaseAddress = new Uri(url);
            request.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.lastToken.access_token);
            return request;
        }
        public static CloudDriveListResponse<CloudDriveFolder> getFolders(ConfigOperations.ConfigData config, String id)
        {
            return listSearch<CloudDriveFolder>(config, id.Length > 0 ? "nodes/" + id + "/children?filters=kind:FOLDER" : "nodes?filters=kind:FOLDER");
        }
        public static CloudDriveListResponse<CloudDriveFolder> getChildFolderByName(ConfigOperations.ConfigData config, String parentId, String name)
        {
            if (String.IsNullOrWhiteSpace(parentId) || String.IsNullOrWhiteSpace(name)) return new CloudDriveListResponse<CloudDriveFolder>();
            return listSearch<CloudDriveFolder>(config, "nodes/" + parentId + "/children?filters=kind:FOLDER AND name:" + name);
        }
        public static CloudDriveListResponse<CloudDriveFolder> getFoldersByName(ConfigOperations.ConfigData config, String name)
        {
            return listSearch<CloudDriveFolder>(config, "nodes?filters=kind:FOLDER AND name:" + name);
        }
        public static CloudDriveListResponse<CloudDriveFolder> getRootFolder(ConfigOperations.ConfigData config, String name)
        {
            return listSearch<CloudDriveFolder>(config, "nodes?filters=kind:FOLDER AND isRoot:true");
        }
        public static CloudDriveFolder getFolder(ConfigOperations.ConfigData config, String id)
        {
            return nodeSearch<CloudDriveFolder>(config, "nodes/" + id);
        }
        public static CloudDriveListResponse<CloudDriveFile> getFileByNameAndParentId(ConfigOperations.ConfigData config, String parentId, String name)
        {
            return listSearch<CloudDriveFile>(config, "nodes/" + parentId + "/children?filters=kind:FILE AND name:" + name);
        }
        public static CloudDriveListResponse<CloudDriveFile> getFilesByName(ConfigOperations.ConfigData config, String name)
        {
            return listSearch<CloudDriveFile>(config, "nodes?filters=kind:FILE AND name:'" + name + "'");
        }
        public static CloudDriveListResponse<CloudDriveFile> getFileByNameAndMd5(ConfigOperations.ConfigData config, String name, String md5)
        {
            return listSearch<CloudDriveFile>(config, "nodes?filters=kind:FILE AND name:'" + name + "' AND contentProperties.md5:" + md5);
        }
        public static CloudDriveFile getFile(ConfigOperations.ConfigData config, String id)
        {
            return nodeSearch<CloudDriveFile>(config, "nodes/" + id);
        }

        public static String uploadFile(ConfigOperations.ConfigData config, string fullFilePath, string parentId)
        {   return uploadFile(config, fullFilePath, parentId, false); }
        public static String uploadFile(ConfigOperations.ConfigData config, string fullFilePath, string parentId, Boolean force)
        {

            var parentList = new List<String>();
            parentList.Add(parentId);

            Dictionary<string, Object> addNode = new Dictionary<string, Object>() { { "name", Path.GetFileName(fullFilePath) }, { "kind", "FILE" }, {"parents",parentList} };
            String myMetaData = JsonConvert.SerializeObject(addNode, Newtonsoft.Json.Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, });
            using (FileStream file = File.Open(fullFilePath, FileMode.Open, FileAccess.Read))
            {
                MultipartFormDataContent form = new MultipartFormDataContent();
                form.Add(new StringContent(myMetaData), "metadata");

                Download myDownload = new Download();
                //var fileStreamContent = new ProgressableStreamContent(, 8096, myDownload);
                var fileStreamContent = new StreamContent(file);
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue(MimeTypeMap.MimeTypeMap.GetMimeType(Path.GetExtension(fullFilePath)));
                form.Add(fileStreamContent, "content", Path.GetFileName(fullFilePath));

                HttpClient request = createAuthenticatedClient(config, config.metaData.contentUrl);
                request.Timeout = new TimeSpan(3,0,0);
                var postAsync = request.PostAsync("nodes" + (force ? "?suppress=deduplication":""), form);
                while (!postAsync.IsCompleted)
                {
                    if (file.CanRead) Console.WriteLine("{0}: {1:P2} uploaded ({2}/{3})", Path.GetFileName(fullFilePath),(double)file.Position/(double)file.Length, file.Position, file.Length);
                    Thread.Sleep(5000);
                }
                Console.WriteLine("{0}: uploaded", Path.GetFileName(fullFilePath));

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


        public static void addNodeParent(ConfigOperations.ConfigData config, string nodeId, string parentId)
        {
            nodeChange<CloudDriveFolder>(config, "nodes/" + parentId + "/children/" + nodeId, new StringContent(""));
        }

        public static void uploadFileContent(ConfigOperations.ConfigData config, string localFilename, string p)
        {
            throw new NotImplementedException();
        }
    }
}
