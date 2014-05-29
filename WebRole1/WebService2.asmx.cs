using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for WebService2
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class WebService2 : System.Web.Services.WebService
    {
        public static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
        public static CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
        public static CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
        private static Dictionary<string, List<AzureTableWebRole>> cache = new Dictionary<string, List<AzureTableWebRole>>();

        [WebMethod]
        public void startCrawl()
        {
            
            CloudQueue queue = queueClient.GetQueueReference("commandq");
            queue.CreateIfNotExists();

            CloudQueueMessage message = new CloudQueueMessage("start");
            queue.AddMessage(message);
        }

        [WebMethod]
        public void stopCrawl()
        {
            CloudQueue queue = queueClient.GetQueueReference("commandq");
            queue.CreateIfNotExists();

            CloudQueueMessage message = new CloudQueueMessage("stop");
            queue.AddMessage(message);
        }

        [WebMethod]
        public string getRAM()
        {
            CloudTable table = tableClient.GetTableReference("performancetable");
            TableQuery<StatTest123> query = new TableQuery<StatTest123>().Select(new string[] { "ram" });

            foreach (StatTest123 entity in table.ExecuteQuery(query))
            {
                return entity.ram;
            }

            return "Nothing found";
        }

        [WebMethod]
        public string getIndexSize()
        {
            CloudTable table = tableClient.GetTableReference("performancetable");
            TableQuery<StatTest123> query = new TableQuery<StatTest123>().Select(new string[] { "count" });
            query.Take(10);

            foreach (StatTest123 entity in table.ExecuteQuery(query))
            {
                return entity.count;
            }

            return "Nothing found";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getTenURL()
        {
            string rowKeyToUse = string.Format("{0:D19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks);
            CloudTable table = tableClient.GetTableReference("titleindextable");
            var query = (from urltable in table.CreateQuery<AzureTableWebRole>()
                         where urltable.PartitionKey == "URL Partition"
                         && urltable.RowKey.CompareTo(rowKeyToUse) > 0
                         select urltable).Take(10);

            List<string> list = new List<string>();
            try
            {
                foreach (AzureTableWebRole entity in query)
                {
                    string url = System.Net.WebUtility.UrlDecode(entity.RowKey);
                    string title = entity.PageTitle;
                    list.Add(url + ";;;" + title);

                }
                return new JavaScriptSerializer().Serialize(list);
            }
            catch
            {
                return "nothing";
            }
        }
        
        [WebMethod]
        public int? getQueueLength()
        {
            CloudQueue queue = queueClient.GetQueueReference("linkq");

            queue.FetchAttributes();
            //queue.
            int? cachedMessageCount = queue.ApproximateMessageCount;
            return cachedMessageCount;
        }


        [WebMethod]
        public string getCPU()
        {
            CloudTable table = tableClient.GetTableReference("performancetable");
            TableQuery<StatTest123> query = new TableQuery<StatTest123>().Select(new string[] { "cpu" });

            foreach (StatTest123 entity in table.ExecuteQuery(query))
            {
                return entity.cpu;
            }

            return "Nothing found";
        }

       

   
        
    }//Closes class
}//Closes namespace
