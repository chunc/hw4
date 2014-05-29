using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerRole1
{
    class StatTable : TableEntity
    {
        public StatTable(string PartitionName, string row)
        {
            this.PartitionKey = PartitionName;
            this.RowKey = row;
        }

        public StatTable() { }
        public string cpu { get; set; }
        public string ram { get; set; }
        public string count { get; set; }
    }
}