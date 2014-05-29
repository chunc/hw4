using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Configuration;
using Microsoft.WindowsAzure.Storage.Table;
using System.IO;
using System.Text.RegularExpressions;


namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        public static List<string> cnn_disallow;
        public static List<string> cnn_sitemap;
        public static List<string> si_disallow;
        public static List<string> si_sitemap;

        private static Dictionary<string, string> visitedURLMain = new Dictionary<string, string>();
        private static Dictionary<string, string> visitedURLSub = new Dictionary<string, string>();

        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter; 

        
     
        public override void Run()
        {
            cpuCounter = new PerformanceCounter();
            cpuCounter.CategoryName = "Processor";
            cpuCounter.CounterName = "% Processor Time";
            cpuCounter.InstanceName = "_Total";
            ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            //Establish Azure Storage Connection
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);

            //Connect to URLqueue and Commandqueue
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue linkQueue = queueClient.GetQueueReference("linkq");
            linkQueue.CreateIfNotExists();
            CloudQueue commandQueue = queueClient.GetQueueReference("commandq");
            commandQueue.CreateIfNotExists();

            //Connect to AzureTable
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("titleindextable");
            table.CreateIfNotExists();
            CloudTable table_counter = tableClient.GetTableReference("performancetable");
            table_counter.CreateIfNotExists();

            //Create sitemaps and disallow lists from robots.txt
            string cnn_robot = "http://www.cnn.com/robots.txt";
            string sports_robot = "http://sportsillustrated.cnn.com/robots.txt";

            List<string> cnn_disallow = parseRobot(cnn_robot, true);
            List<string> cnn_sitemap = parseRobot(cnn_robot, false);
            List<string> si_disallow = parseRobot(sports_robot, true);
            List<string> si_sitemap = parseRobot(sports_robot, false);

            bool go = true;

            while (true)
            {
                //Update performance counters
                StatTable myCounter = new StatTable("counter", "one");
                TableOperation retrieveOperation = TableOperation.Retrieve<StatTable>("counter", "one");
                TableResult retrievedResult = table_counter.Execute(retrieveOperation);
                StatTable updateStat = (StatTable)retrievedResult.Result;
                if (updateStat != null)
                {
                    updateStat.cpu = cpuCounter.NextValue().ToString() + "%";
                    updateStat.ram = ramCounter.NextValue().ToString() + " MB";
                    updateStat.count = visitedURLMain.Count.ToString();
                    TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(updateStat);
                    table_counter.Execute(insertOrReplaceOperation);
                    Trace.TraceInformation("CPU used is: " + cpuCounter.NextValue() + "% RAM: " + ramCounter.NextValue() + " MB");
                }

                //Pop out command message from Queue
                CloudQueueMessage checkCommandQ = commandQueue.GetMessage(TimeSpan.FromMinutes(5));
                if (checkCommandQ != null)
                {
                    string command = checkCommandQ.AsString;
                    if (command == "start")
                    {
                        //CloudQueue linkQueue = queueClient.GetQueueReference("linkq");
                        //linkQueue.CreateIfNotExists();
                        string[] list = getSitemap();
                        foreach (string item in list)
                        {
                            CloudQueueMessage messagePush = new CloudQueueMessage(item);
                            linkQueue.AddMessage(messagePush);
                        }
                        go = true;
                    }
                    else if (command == "stop")
                    {
                        linkQueue.Clear();
                        visitedURLMain.Clear();
                        visitedURLSub.Clear();
                        
                        go = false;
                    }
                    //commandQueue.DeleteMessage(checkCommandQ); //Delete message
                } //Close Command Queue Layer

                if (go)
                {
                    CloudQueueMessage linkPop = linkQueue.GetMessage(TimeSpan.FromMinutes(5));

                    if (linkPop != null)
                    {
                        Trace.TraceInformation("Popped New Message");
                        string url = linkPop.AsString;
                        linkQueue.DeleteMessage(linkPop); //Delete message
                        if ((isDisallowed(url, cnn_disallow) == false || isDisallowed(url, si_disallow)==false) && visitedURLMain.ContainsKey(url) == false)
                        {
                            string htmlsource = getPageSource(url);
                            string pagetitle = getTitle(htmlsource);
                            string datestring = getDate(htmlsource);
                            visitedURLMain.Add(url, pagetitle);

                            try
                            {
                                if (pagetitle != "")
                                {
                                    AzureTableWorkerRole tableEntry = new AzureTableWorkerRole();
                                    String[] titleindex = Regex.Split(pagetitle, " ");
                                    foreach (string item in titleindex)
                                    {
                                        bool insert = false;
                                        string word = item.ToLower();
                                        string regex_word = @"\b[\da-z]+\b";
                                        var root = Regex.Match(word, regex_word, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                        if (root.Length != 0)
                                        {
                                            tableEntry.PartitionKey = root.Value;
                                            insert = true;
                                           
                                            tableEntry.RowKey = System.Net.WebUtility.UrlEncode(url);
                                            tableEntry.PageTitle = pagetitle;
                                            tableEntry.DateString = datestring;
                                            //insert = true;
                                            
                                            if (insert)
                                            {
                                                TableOperation insertOperation = TableOperation.Insert(tableEntry);
                                                table.Execute(insertOperation);
                                                Trace.TraceInformation("Added to AzureTable: "+pagetitle);
                                                Trace.TraceInformation("Word: " + word);
                                                Trace.TraceInformation("URL: " + url);
                                            }

                                        }
                                    }
                                }
                            }
                            catch (StorageException ex)
                            {
                                Trace.TraceInformation("Unable add to AzureTable: ");
                                Trace.TraceInformation("EX PageTitle: "+pagetitle);
                                Trace.TraceInformation("EX URL: "+url);
                                
                            }

                            //Crawl for new links
                            string regex_href = @"href=\""(.*?)\"""; //Extracts href or url content
                            var matches = Regex.Matches(htmlsource, regex_href, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                            foreach (Match item in matches)
                            {
                                string link = item.Groups[1].Value;
                                string link_new = checkTotalValidity(link);
                                CloudQueueMessage messagePush = new CloudQueueMessage(link_new);
                                if (link_new != "" && visitedURLSub.ContainsKey(link_new) == false)
                                {
                                    Trace.TraceInformation("Added to linkqueue: " + link_new);
                                    linkQueue.AddMessage(messagePush);
                                    visitedURLSub.Add(link_new, "");
                                }
                            }

                        } //Close if(Disallowed)
                    } //Close (linkPop != null)
                } //Close if(go) layer

                Thread.Sleep(500);
                Trace.TraceInformation("Working");
            } //Closes while(true)
        } //Closes Run

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            return base.OnStart();
        }

        /// <summary>
        /// Retrieve the entire html code
        /// </summary>
        /// <param name="url">url</param>
        /// <returns>string of html code</returns>
        public string getPageSource(string url)
        {
            try
            {
                WebRequest request = WebRequest.Create(url);
                WebResponse response = request.GetResponse();
                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream);
                string htmlText = reader.ReadToEnd();

                return htmlText;
            }
            catch
            {
                return "not valid url";
            }

        }

        /// <summary>
        /// Checks if href found is same domain as cnn, is no more than 2 months old, and is a html file
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string checkTotalValidity(string url)
        {
            string root = "http://www.cnn.com";
            if (IsAWebPage(url) == true && url.Contains("http://") == false)
            {
                url = root + url;
                //Trace.TraceInformation("New Link is called: " + url);
            }
            if (IsAWebPage(url) == false) return "";
            if (isSameDomain(url) == false) return "";
            if (containsDate(url) == false) return "";

            return url;
        }

        /// <summary>
        /// Makes sure url listed on robot.txt is not crawled
        /// </summary>
        /// <param name="url">string url</param>
        /// <param name="disallow_list">robots.txt disallow list</param>
        /// <returns></returns>
        public bool isDisallowed(string url, List<string> disallow_list)
        {
            string regex_root = @"http:\/\/.*cnn.com";
            try
            {
                var root = Regex.Match(url, regex_root, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (root.Success)
                {
                    foreach (string item in disallow_list)
                    {
                        string dir = root.ToString() + item.Trim();
                        if (url.Contains(dir)) return true;
                    }
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch
            {
                Trace.TraceInformation("Exception in Disallow List Check");
                return true;
            }
        }

        /// <summary>
        /// Checks if href found is a html file
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static bool IsAWebPage(string url)
        {
            if (url.Contains(".htm")) return true;
            return false;
        }

        /// <summary>
        /// Checks if href found is less than 60 days old
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static bool containsDate(string url)
        {
            string regex_date = @"(\d\d\d\d\/\d\d\/\d\d)";
            var match = Regex.Match(url, regex_date, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            string datestring = match.ToString();
            if (match.Success)
            {
                DateTime today = DateTime.Today;
                char[] seperator = { '/' };
                string[] dateArray = datestring.Split(seperator);
                DateTime webpageDate = new DateTime(Convert.ToInt16(dateArray[0]), Convert.ToInt16(dateArray[1]), Convert.ToInt16(dateArray[2]), 0, 0, 0);
                TimeSpan time = today - webpageDate;
                int days = (int)Math.Round((today - webpageDate).TotalDays);
                if (days < 60)
                {
                    return true;
                }
                Trace.TraceInformation("Too old");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if href found is less than 60 days old
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static bool isNewDate(string source)
        {
            string htmltext = source;
            string regex = @"(\d\d\d\d-\d\d-\d\d)"; //Extracts Date webpage

            var matches = Regex.Matches(htmltext, regex, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var links = new List<string>();

            foreach (Match item in matches)
            {
                string link = item.Groups[1].Value;
                links.Add(link);
                break;
            }
            if (links.Count == 0) return false;

            string datestring = links[0];
            DateTime today = DateTime.Today;
            char[] seperator = { '-' };
            string[] dateArray = datestring.Split(seperator);
            DateTime webpageDate = new DateTime(Convert.ToInt16(dateArray[0]), Convert.ToInt16(dateArray[1]), Convert.ToInt16(dateArray[2]), 0, 0, 0);
            TimeSpan time = today - webpageDate;
            int days = (int)Math.Round((today - webpageDate).TotalDays);
            if (days < 60)
            {
                return true;
            }
            Trace.TraceInformation("Too old");
            return false;
        }

        /// <summary>
        /// Checks if href found is same as the cnn root domain
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static bool isSameDomain(string url)
        {
            string regex_root = @"http:\/\/.*cnn.com";
            var root = Regex.Match(url, regex_root, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (root.Success)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Parses robots.txt and stores sitemaps and disallows in a list
        /// </summary>
        /// <param name="url"></param>
        /// <param name="disallow"></param>
        /// <returns></returns>
        public List<string> parseRobot(string url, bool disallow)
        {
            string robot = getPageSource(url);
            List<string> List = new List<string>();
            String[] lines = Regex.Split(robot, "\n");
            string delimiter;
            string delimiter2;
            if (disallow == true)
            {
                delimiter = "Disallow:";
                delimiter2 = ":";
            }
            else
            {
                delimiter = "Sitemap:";
                delimiter2 = " ";
            }

            foreach (String item in lines)
            {
                if (item.Contains(delimiter))
                {
                    String[] text = Regex.Split(item, delimiter2);
                    List.Add(text[1]);
                }
            }
            return List;
        }

        /// <summary>
        /// Fetches the content of a title tag from the html source
        /// </summary>
        /// <param name="htmlsource"></param>
        /// <returns></returns>
        public string getTitle(string htmlsource)
        {
            string regex_title = @"(?<=<title.*>)([\s\S]*)(?=</title>)";
            var title = Regex.Match(htmlsource, regex_title, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (title.Success)
            {
                return title.ToString();
            }
            //return "No Page Title Found";
            return "";
        }

        /// <summary>
        /// Returns date scraped from meta data
        /// </summary>
        /// <param name="htmlText"></param>
        /// <returns>String that contains date (e.g. 2014-27-04)</returns>
        public string getDate(string htmlText)
        {
            string regex = @"(\d\d\d\d-\d\d-\d\d)"; //Extracts Date webpage
            var match = Regex.Match(htmlText, regex, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match != null)
            {
                return match.Value;
            }

            return "";
        }

        public string[] getSitemap()
        {
            string[] list = { "http://sportsillustrated.cnn.com/nba/", "http://www.cnn.com/video", "http://www.cnn.com/US", "http://www.cnn.com/WORLD",
                            "http://www.cnn.com/POLITICS","http://www.cnn.com/JUSTICE","http://www.cnn.com/SHOWBIZ","http://www.cnn.com/TECH","http://www.cnn.com/HEALTH",
                            "http://www.cnn.com/LIVING","http://www.cnn.com/TRAVEL","http://www.cnn.com/OPINION","http://ireport.cnn.com","http://money.cnn.com/"
                            };
            return list; 
        }




    } //Close WorkerRole
} //Close Namespace
