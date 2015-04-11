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

namespace AmazonCloudDriveSync
{
    public class ConfigOperations
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
            public ConfigData()
            {
                lastToken = new AuthTokenResponse();
                metaData = new MetaDataResponse();
            }
            public void updateConfig(Action saveConfig)
            {
                if (this.lastTokenReceived.AddSeconds(this.lastToken.expires_in) < DateTime.Now)
                    if (String.IsNullOrWhiteSpace(this.lastToken.access_token))
                        getBrandNewToken();
                    else
                        refreshAccessToken(this.lastToken.refresh_token, ConfigurationManager.AppSettings["appKey"], ConfigurationManager.AppSettings["appSecret"]);
                if (String.IsNullOrWhiteSpace(this.lastToken.access_token) && (this.lastTokenReceived.AddSeconds(this.lastToken.expires_in) < DateTime.Now))
                    getBrandNewToken();
                saveConfig();

                if (this.lastMetaDataCheck.AddDays(3) < DateTime.Now)
                    getMetaDataUrl();
                if (String.IsNullOrWhiteSpace(this.rootFolderId))
                    getRootFolderId();

                if (String.IsNullOrWhiteSpace(this.cloudMainFolderId) || this.cloudMainFolderId == this.rootFolderId)
                {
                    var possibleMainFolders = CloudDriveOperations.getFoldersByName(this, ConfigurationManager.AppSettings["cloudFolder"]);
                    if (possibleMainFolders.count > 1) throw new NotImplementedException();
                    if (possibleMainFolders.count == 0)
                    {
                        this.cloudMainFolderId = CloudDriveOperations.createFolder(this, ConfigurationManager.AppSettings["cloudFolder"], this.rootFolderId);
                        
                    }
                    else if (possibleMainFolders.count == 1)
                        this.cloudMainFolderId = possibleMainFolders.data[0].id;
                }
                saveConfig();

            }
            private void getRootFolderId()
            {
                CloudDriveOperations.CloudDriveFolder x = CloudDriveOperations.getFolders(this, "").data[0];
                String newParent = x.parents[0];
                while (String.IsNullOrWhiteSpace(this.rootFolderId))
                {
                    CloudDriveOperations.CloudDriveFolder y = CloudDriveOperations.getFolder(this, newParent);
                    if (y.parents.Count > 0)
                        newParent = y.parents[0];
                    else
                        this.rootFolderId = y.id;
                }
            }
            private void getBrandNewToken()
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
                //String mycontent = content.ReadAsStringAsync().Result;
                Task<HttpResponseMessage> responseTask = reqAccessToken.PostAsync("auth/o2/token", content);
                HttpResponseMessage response = responseTask.Result;

                //reqAccessToken.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                return response.Content.ReadAsStringAsync().Result;
                //reqAccessToken.
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
}
