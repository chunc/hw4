using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerRole1
{
    class AzureTableWorkerRole : TableEntity
    {
        public AzureTableWorkerRole(string PartitionName)
        {
            this.PartitionKey = PartitionName;
        }

        public AzureTableWorkerRole() { }
        public string url { get; set; }
        public string PageTitle { get; set; }
        public string DateString { get; set; }
    }
}
