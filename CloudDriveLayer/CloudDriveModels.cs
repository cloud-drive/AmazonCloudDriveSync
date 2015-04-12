using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudDriveLayer.CloudDriveModels
{

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
        public class CloudDriveNode : CloudDriveNodeRequest
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
