using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for WebService
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line.
    [System.Web.Script.Services.ScriptService]
    public class WebService1 : System.Web.Services.WebService
    {
        public static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
        public static CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
        public static CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
        private static Dictionary<string, List<AzureTableWebRole>> cache = new Dictionary<string, List<AzureTableWebRole>>();

        public static TrieStuff s = new TrieStuff();
        /// <summary>
        /// This method downloads the wiki data from blob
        /// </summary>
        /// <returns></returns>
        [WebMethod]
        public bool DownloadFileFromBlob()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("hw2");

            CloudBlockBlob blockBlob = container.GetBlockBlobReference("new_wiki_clean");
            if (blockBlob.Exists())
            {
                //string url = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToString() + "\\new_wiki_clean";
                string filePath = HostingEnvironment.ApplicationPhysicalPath + "new_wiki_clean";
                using (var fileStream = System.IO.File.OpenWrite(@filePath))
                //using (var fileStream = System.IO.File.OpenWrite(@"C:\Users\Chun-Cheng Chang\Downloads\test123\file52"))
                {
                    blockBlob.DownloadToStream(fileStream);
                }
                return true;
            }
            else
            {
                return false;
            }
        }


        /// <summary>
        /// Reads the wiki data line by line and builds a Trie structure
        /// </summary>
        [WebMethod]
        public string buildTrieStructure()
        {
            float memCheck_base = GetAzureMem();
            float memCheck_now;

            //string url = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToString() + "\\new_wiki_clean";
            string filePath = HostingEnvironment.ApplicationPhysicalPath + "new_wiki_clean";
            using (StreamReader sr = new StreamReader(@filePath))
            //using (StreamWriter sw = new StreamWriter(@url2))
            {
                sr.ReadLine();
                int line_count = 0;
                while (sr.EndOfStream == false)
                {
                    if (line_count > 500)
                    {
                        memCheck_now = GetAzureMem();
                        if (memCheck_base > 1000 && GetAzureMem() < memCheck_base - 1000 + 250) 
                        {

                            break;
                        }
                        if(memCheck_base < 1000 && memCheck_now < 1000-memCheck_base+50)
                        {
                            break;
                        }
                        line_count = 0;
                    }
                    line_count++;

                    //memCheck_now = GetAzureMem();
                    //if (memCheck_now < 300) { break; }
                    string line = sr.ReadLine();
                    if (Regex.IsMatch(line, "^[a-zA-Z_]+$") == true)
                    {
                        //sw.WriteLine(line);
                        s.addToTrie(line.ToLower());
                    }
                }
            }
            return "finished building Trie";
        }

        /// <summary>
        /// Returns result from Trie traversal
        /// </summary>
        /// <param name="input">Ajax keystroke string input</param>
        /// <returns>result list in JSON format</returns>
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getSearchResult(string input)
        {
            //buildTrieStructure();
            List<string> result = new List<string>();
            result = s.searchPrefix(input);
            return new JavaScriptSerializer().Serialize(result);
        }


        /// <summary>
        /// Notifies how much memory is available in Azure
        /// </summary>
        private PerformanceCounter memProcess = new PerformanceCounter("Memory", "Available MBytes");
        [WebMethod]
        public float GetAzureMem()
        {
            float memUsage = memProcess.NextValue();
            return memUsage;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string queryTableIndex(string input)
        {
            CloudTable table = tableClient.GetTableReference("titleindextable");
            String[] querysplit = Regex.Split(input, " ");

            List<AzureTableWebRole> wordresult = new List<AzureTableWebRole>();
            List<string> templist = new List<string>();

            foreach (string word in querysplit)
            {
                //Check if search words have been cached
                if (!cache.ContainsKey(word))
                {
                    try
                    {
                        //Execute Azure table query code
                        TableQuery<AzureTableWebRole> rangeQuery = new TableQuery<AzureTableWebRole>()
                        .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, word));
                        cache.Add(word, new List<AzureTableWebRole>());
                        foreach (AzureTableWebRole entity in table.ExecuteQuery(rangeQuery))
                        {
                            AzureTableWebRole bla = entity;
                            wordresult.Add(entity);
                            cache[word].Add(entity);
                        }
                    }
                    catch
                    {
                        
                    }
                    
                }
                else
                {
                    //Return results already stored in cache
                    List<AzureTableWebRole> temp1 = cache[word];
                    foreach (AzureTableWebRole item in temp1)
                    {
                        wordresult.Add(item);
                    }
                }
            }

            //for (int i = 0; i < querysplit.Length; i++)
            //{       
            //    TableQuery<AzureTableWebRole> rangeQuery = new TableQuery<AzureTableWebRole>()
            //    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, querysplit[i])); 

            //    foreach (AzureTableWebRole entity in table.ExecuteQuery(rangeQuery))
            //    {
            //        AzureTableWebRole bla = entity;
            //        wordresult.Add(entity);
            //        cache[querysplit[i]].Add(entity);
            //    }
            //}

            var linq_query = wordresult
                .OrderByDescending(x => x.DateString)
                .GroupBy(w => w.PageTitle)
                .OrderByDescending(w => w.Count())
                .Select(y => y.First()).Take(25)
                ;

            foreach (var item in linq_query)
            {
                string url = System.Net.WebUtility.UrlDecode(item.RowKey);
                string title = item.PageTitle;
                string dateS = item.DateString;
                templist.Add(url + ";;;" + title + ";;;" + dateS);
            }

            return new JavaScriptSerializer().Serialize(templist);
        }

        
    }//Closes Class

}//Closes Namespace