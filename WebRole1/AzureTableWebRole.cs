﻿using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebRole1
{
    class AzureTableWebRole : TableEntity
    {
        public AzureTableWebRole(string PartitionName)
        {
            this.PartitionKey = PartitionName;
            //this.RowKey = key;
        }

        public AzureTableWebRole() { }
        public string url { get; set; }
        public string PageTitle { get; set; }
        public string DateString { get; set; }
    }
}