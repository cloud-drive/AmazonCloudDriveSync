using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Temboo.Core;
using Temboo.Library.Amazon.CloudDrive.Folders;
using Temboo.Library.Amazon.LoginWithAmazon;

namespace AmazonCloudDriveSync
{
    class Program
    {
        static void Main(string[] args)
        {
            // Instantiate the Choreo, using a previously instantiated TembooSession object, eg:
            TembooSession session = new TembooSession("everyonce", "myFirstApp", "8fea6aead1d64494bef875399fd5fc66");
            /*InitializeOAuth initializeOAuthChoreo = new InitializeOAuth(session);

            // Set inputs
            initializeOAuthChoreo.setClientID("amzn1.application-oa2-client.7e28805bdfa44d39924c98f818775a52");
            initializeOAuthChoreo.setScope("clouddrive:read clouddrive:write");

            // Execute Choreo
            InitializeOAuthResultSet initializeOAuthResults = initializeOAuthChoreo.execute();

            // Print results
            Console.WriteLine(initializeOAuthResults.AuthorizationURL);
            Console.WriteLine(initializeOAuthResults.CallbackID);
            Console.ReadKey();
            */
            /*
            FinalizeOAuth finalizeOAuthChoreo = new FinalizeOAuth(session);

            // Set inputs
            finalizeOAuthChoreo.setCallbackID("everyonce/8c51ca21-f753-4ae1-8fe5-a1ff017f2968"); //get from user
            finalizeOAuthChoreo.setClientSecret("4a5396245bb85292b3ee4c3769f1fcce2bf6014032edc37d128d2dfdff72799d");
            finalizeOAuthChoreo.setClientID("amzn1.application-oa2-client.7e28805bdfa44d39924c98f818775a52");

            // Execute Choreo
            FinalizeOAuthResultSet finalizeOAuthResults = finalizeOAuthChoreo.execute();

            // Print results
            Console.WriteLine(finalizeOAuthResults.AccessToken);
            Console.WriteLine(finalizeOAuthResults.ErrorMessage);
            Console.WriteLine(finalizeOAuthResults.Expires);
            Console.WriteLine(finalizeOAuthResults.RefreshToken);
            Console.ReadKey();
             * */
            String authToken = "Atza|IQEBLzAtAhUAg84nGAj2VaO2Nb7JtDH5L-dETPICFFPvkMWe5XT2iar0HhXdpMspQu9Vz5H9rbhMnTinDJvl17Aj-rN2hy4tYrrCLfOUICnB5r_DHkphbKT5-hPVQDj-SZEbh50t3giigBEYasc9xdNUvUbd6YWmts6dvJMQU4Ls6eHIIg8Gmf5PrX6P7Xz-dJthQLt0OkrG91_HhYokmG_HheRWVztXbYtY8N-IRk93q77OFI-fCP3F-OTo4tAxh4BEWqv8LXIWGXxvlaVriHm62gaD34FhdW0n8Tc2is1X5yIg1F8WFT8Zbe0KByqfle6QD6AWG9OIu5c_GTxkOYIDijCULIGFZjmjeC1NdOPnfEZK0XRBqr79ZkH70rqo8gboyF6WgdZ4vgQnC1ayiD6PEh62Yei6H9B0V6fFloEiFPdD";
            


            // Set inputs

            // Print results
            Console.WriteLine(listFoldersResults.Response);
            Console.WriteLine(listFoldersResults.NewAccessToken);
            


            Console.ReadKey();
        }
        String getRootFolderId(TembooSession session, String authToken)
        {
            String finalRootId = "";
            ListFolders listFoldersChoreo = new ListFolders(session);
            listFoldersChoreo.setCredential("AmazonCloudDriveAccount");
            listFoldersChoreo.setAccessToken(authToken);
            listFoldersChoreo.setLimit("1");
            ListFoldersResultSet listFoldersResults = listFoldersChoreo.execute();

            var results = JsonConvert.DeserializeObject<dynamic>(listFoldersResults.Response);
            var firstData = results.data[0];

            GetFolderMetadata getFolderMetaData = new GetFolderMetadata(session);
            getFolderMetaData.setID(firstData.parents[0].ToString());
            getFolderMetaData.setCredential("AmazonCloudDriveAccount");
            getFolderMetaData.setAccessToken(authToken);
            GetFolderMetadataResultSet result2 = getFolderMetaData.execute();

            var mySecondFolder = JsonConvert.DeserializeObject<dynamic>(result2.Response);
            Console.WriteLine("count of data: {0}", results.data.Count);

        }
    }
}
