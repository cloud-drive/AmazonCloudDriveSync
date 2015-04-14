using CloudDriveLayer.CloudDriveModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CloudDriveLayer.ConfigOperations
{
    public class ConfigData
    {
        public AuthTokenResponse lastToken { get; set; }
        public DateTime lastTokenReceived { get; set; }
        public String cloudDriveLocalDirectory { get; set; }
        public String rootFolderId { get; set; }
        public String cloudMainFolderId { get; set; }
        public MetaDataResponse metaData { get; set; }
        public DateTime lastMetaDataCheck { get; set; }
        public String _appKey;
        public String _appSecret;
        public String _cloudMainFolderName;
        public String _oauthxRedirect;
        public String _oauthxBase;
        public ConfigData(String appKey, String appSecret, String cloudRoot, String oauthXRedirect, String oauthXBase)
        {
            _appKey = appKey;
            _appSecret = appSecret;
            _cloudMainFolderName = cloudRoot;
            _oauthxRedirect = oauthXRedirect;
            _oauthxBase = oauthXBase;
            lastToken = new AuthTokenResponse();
            metaData = new MetaDataResponse();
        }
        public void updateTokens(Action saveConfig)
        {
            if (this.lastTokenReceived.AddSeconds(this.lastToken.expires_in) < DateTime.Now)
            {
                if (String.IsNullOrWhiteSpace(this.lastToken.access_token))
                    waitForAuth(getBrandNewToken());
                else
                    refreshAccessToken(this.lastToken.refresh_token, _appKey, _appSecret);
                saveConfig();
            }
            if (String.IsNullOrWhiteSpace(this.lastToken.access_token) && (this.lastTokenReceived.AddSeconds(this.lastToken.expires_in) < DateTime.Now))
            {
                getBrandNewToken();
                saveConfig();
            }
        }
        public void updateConfig(Action saveConfig)
        {
            updateTokens(saveConfig);

            if (lastMetaDataCheck.AddDays(3) < DateTime.Now)
                getMetaDataUrl();
            getRootFolderId();

            if (String.IsNullOrWhiteSpace(cloudMainFolderId) || cloudMainFolderId == rootFolderId)
            {
                if (String.IsNullOrWhiteSpace(rootFolderId))
                    rootFolderId = CloudDriveOperations.getRootFolder(this, _cloudMainFolderName).data[0].id;
                string[] pathExplode = _cloudMainFolderName.Split(new char[] { '\\' });
                var currentRoot = rootFolderId;
                foreach (String folderName in pathExplode )
                {
                    var newRoot = CloudDriveOperations.getChildFolderByName(this, currentRoot, folderName);
                    if (newRoot.count == 0)
                        currentRoot = CloudDriveOperations.createFolder(this, folderName, currentRoot);
                    else if (newRoot.count == 1)
                        currentRoot = newRoot.data[0].id;
                    else
                        throw new NotImplementedException();
                }
                cloudMainFolderId = currentRoot;
            }
            saveConfig();

        }
        private void getRootFolderId()
        {
            CloudDriveFolder x = CloudDriveOperations.getFolders(this, "").data[0];
            String newParent = x.parents[0];
            while (String.IsNullOrWhiteSpace(this.rootFolderId))
            {
                CloudDriveFolder y = CloudDriveOperations.getFolder(this, newParent);
                if (y.parents.Count > 0)
                    newParent = y.parents[0];
                else
                    this.rootFolderId = y.id;
            }
        }
        private String getBrandNewToken()
        {
            String loginWithAmazonUrl = String.Format(
                "https://www.amazon.com/ap/oa?client_id={0}&scope=clouddrive%3Aread%20clouddrive%3Awrite&response_type=code&redirect_uri={1}",
                _appKey,
                _oauthxRedirect);
            String newId = Guid.NewGuid().ToString().Replace("{", "").Replace("}", "");
            String actualUrl = String.Format(
                "{0}?id={1}&authType=loginWithAmazon&authUrl={2}",
                _oauthxBase,
                newId,
                Convert.ToBase64String(Encoding.Unicode.GetBytes(loginWithAmazonUrl))
                );
            Process.Start(actualUrl);
            return newId;
        }
        private void waitForAuth(String correlationId)
        {
            WebClient waiter = new WebClient();
            OAuthTransaction finishedAuth = new OAuthTransaction();
            Console.WriteLine("Waiting for authorization...");
            do
            {
                Thread.Sleep(1000);
                finishedAuth = JsonConvert.DeserializeObject<OAuthTransaction>(
                    waiter.DownloadString(_oauthxBase + correlationId));
            } while (!finishedAuth.authComplete);

            Console.WriteLine("Got Code: {0}", finishedAuth.authCode);

            String accessToken = getAccessToken(finishedAuth.authCode, _appKey, _appSecret, _oauthxRedirect);
            AuthTokenResponse accessTokenObj = JsonConvert.DeserializeObject<AuthTokenResponse>(accessToken);
            this.lastToken = accessTokenObj;
            this.lastTokenReceived = DateTime.Now;
        }
        private void getMetaDataUrl()
        {
            HttpClient reqMetaData = new HttpClient();
            reqMetaData.BaseAddress = new Uri("https://drive.amazonaws.com/");
            reqMetaData.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this.lastToken.access_token);
            String mycontent = reqMetaData.GetStringAsync("drive/v1/account/endpoint").Result;
            this.metaData = JsonConvert.DeserializeObject<MetaDataResponse>(mycontent);
            this.lastMetaDataCheck = DateTime.Now;
        }
        private String getAccessToken(string code, string key, string secret, string redirect)
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
            Task<HttpResponseMessage> responseTask = reqAccessToken.PostAsync("auth/o2/token", content);
            HttpResponseMessage response = responseTask.Result;
            return response.Content.ReadAsStringAsync().Result;

        }
        private void refreshAccessToken(string refresh_token, string key, string secret)
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
            this.lastToken = JsonConvert.DeserializeObject<AuthTokenResponse>(response.Content.ReadAsStringAsync().Result);
            this.lastTokenReceived = DateTime.Now;

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
    }

}
