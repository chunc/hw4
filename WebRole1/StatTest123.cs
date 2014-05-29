using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebRole1
{
    class StatTest123 : TableEntity
    {
        public StatTest123(string PartitionName, string row)
        {
            this.PartitionKey = PartitionName;
            this.RowKey = row;
        }

        public StatTest123() { }
        public string cpu { get; set; }
        public string ram { get; set; }
        public string count { get; set; }
    }
}