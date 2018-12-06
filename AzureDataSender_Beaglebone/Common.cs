using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;



namespace TableStorage
{
    // RoSchmi

    public struct TableQueryResponse
    {
        public List<string> ListResult { get; set; }
        public string ErrorMessage { get; set; }
    }

    class Common
    {
        /// <summary>
        /// Validate the connection string information in app.config and throws an exception if it looks like 
        /// the user hasn't updated this to valid values. 
        /// </summary>
        /// <param name="storageConnectionString">Connection string for the storage service or the emulator</param>
        /// <returns>CloudStorageAccount object</returns>
        public static CloudStorageAccount CreateStorageAccountFromConnectionString(string storageConnectionString)
        {
            CloudStorageAccount storageAccount;
            try
            {
                storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            }
            catch (FormatException ex)
            {
#if Debug
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the application.");
#endif
                throw;
            }
            catch (ArgumentException)
            {
#if Debug
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the sample.");
#endif
                throw;
            }

            return storageAccount;
        }


       

        /// </summary>
        /// <param name="table">The sample table name</param>
        /// <param name="entity">The entity to insert or merge</param>
        /// <returns>A Task object</returns>        
        public static async Task<DynamicTableEntity> InsertOrMergeEntityAsync(CloudTable table, ITableEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("Entity was null");
            }

            try
            {
                // Create the InsertOrReplace table operation

                TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(entity);

                // Execute the operation.
                TableResult result = await table.ExecuteAsync(insertOrMergeOperation);

                DynamicTableEntity insertedEntity = result.Result as DynamicTableEntity;

                return insertedEntity;
            }
            catch (StorageException e)
            {
                throw;
            }
        }
       



        /// <summary>
        /// Lists tables in the storage account, optionally whose names begin with the specified prefix.
        /// </summary>
        /// <param name="tableClient">The Table service client object.</param>
        /// <param name="count">The number of tablenames maximally transmitted. </param>
        /// <param name="prefix">The table name prefix. In case of null or "" no prefix is used. </param>
        /// <returns>A Task object</returns>
        public static async Task<TableQueryResponse> ListTablesWithPrefix(CloudTableClient tableClient, int count, string prefix = "")
        {
            TableContinuationToken continuationToken = null;
            TableResultSegment resultSegment = null;
            TableQueryResponse response = new TableQueryResponse { ListResult = null, ErrorMessage = "" };          
            List<string> localTables = new List<string>();

            try
            {
                do
                {
                    // List tables beginning with the specified prefix. 
                    // Passing in null for the maxResults parameter returns the maximum number of results (up to 5000).

                    if ((prefix == "") | (prefix == null))
                    {
                        resultSegment = await tableClient.ListTablesSegmentedAsync(
                            null, count, continuationToken, null, null);

                    }
                    else
                    {
                        resultSegment = await tableClient.ListTablesSegmentedAsync(
                        prefix, count, continuationToken, null, null);
                    }

                    // Enumerate the tables returned.
                    foreach (var table in resultSegment.Results)
                    {
                        localTables.Add(table.Name);
                    }
                }
                while (continuationToken != null);

                response.ListResult = localTables;
                response.ErrorMessage = "";
                return response;
            }
            catch (StorageException e)
            {
                response.ListResult = null;
                response.ErrorMessage = e.Message;
                return response;
            }
        }
    }
}