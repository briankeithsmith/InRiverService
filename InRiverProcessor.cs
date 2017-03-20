using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using ClosedXML;
using ClosedXML.Excel;
using System.IO;





namespace APIModels.InRiver
{
    public class InRiverProcessor
    {

        #region Constants
        public const string PRODUCT_TEMPLATE = "InRiverProductTemplate";
        public const string ITEM_TEMPLATE = "InRiverItemTemplate";
        public const string RELATIONSHIP_TEMPLATE="InRiverRelateTemplate";
        public const string INRIVER_DESTINATIONS = "InRiverDestinations";
        #endregion

        #region PushItemForUpdate
        public static async Task<long> PushItemForUpdate(string companyid, string item)
        {
            return await InRiverProcessorItem.PushItemForUpdate(companyid, item);
        }

        public static async Task<long> PushItemForUpdate(int companyid, string item)
        {
            return await InRiverProcessorItem.PushItemForUpdate(companyid, item);
        }
        #endregion

        #region UpdateState
        public static async Task UpdateState(IEnumerable<InRiverProcessorItem> unprocessed, InRiverState state)
        {
            unprocessed.ForEach(o => o.InRiverState = state);

            IEnumerable<Task<long>> tasks = from InRiverProcessorItem in unprocessed select InRiverProcessorItem.SaveAsync();

            await Task.WhenAll(tasks);      
        }
        #endregion

        #region GetDestinationDirectory
        public static async Task<string> GetDestinationDirectory(InRiverDestination dest)
        {
            string destDirectory = "";
            await Task.Run(() =>
            {
                //get destination folder
                List<CommonLookup> opts = CommonLookup.GetLookups(INRIVER_DESTINATIONS, true);
                
                switch (dest)
                {
                    case InRiverDestination.Products:
                        destDirectory = opts.Where(o => o.Code == "Products").FirstOrDefault().CodeDesc;
                        break;
                    case InRiverDestination.Parts:
                        destDirectory = opts.Where(o => o.Code == "Parts").FirstOrDefault().CodeDesc;
                        break;
                    case InRiverDestination.StockPartRelationship:
                        destDirectory = opts.Where(o => o.Code == "StockPartRelation").FirstOrDefault().CodeDesc;
                        break;
                    default:
                        destDirectory = opts.Where(o => o.Code == "Products").FirstOrDefault().CodeDesc;
                        break;
                }

            });

            return destDirectory;
        }
        #endregion

        #region CopyToDestination
        private static async Task<bool> CopyToDestination(string filename, InRiverDestination dest)
        {
            if (!File.Exists(filename)) { return false; }

            Boolean result = false;

            string dir = await GetDestinationDirectory(dest);

            try
            {
                File.Copy(filename, dir + filename);
            }
            catch (UnauthorizedAccessException ex1)
            {
                return result;
            }
            catch (DirectoryNotFoundException ex2)
            {
                return result;
            }
            catch (Exception ex3)
            {
                return result;
            }

            result = true;

            return result;
        }
        #endregion

        #region ClearExistingFiles
        private static void ClearExistingFiles()
        {
            IEnumerable<string> files = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.xlsx");

            foreach (string f in files)
            {
                File.Delete(f);
            }
        }
        #endregion
        #region ProcessUpdates
        public static async Task<InRiverServiceResult> ProcessUpdates()
        {
            InRiverServiceResult sr = new InRiverServiceResult();

            await Task.Run(() =>
            {
                ClearExistingFiles();
            });
          
            var unprocessed = await InRiverProcessorItem.GetUnprocessed();

            var unprocessedStockNumbers = unprocessed.Where(o => o.CompanyID == 9);

            var unprocessedParts = unprocessed.Where(o => o.CompanyID != 9);

            await CheckSQLForItems(unprocessedStockNumbers, InRiverDestination.Products);

            await CheckSQLForItems(unprocessedParts, InRiverDestination.Parts);

            await UpdateState(unprocessed, InRiverState.InProcess);

            await ProcessDataUpdates(unprocessedStockNumbers, InRiverDestination.Products);

            await ProcessDataUpdates(unprocessedParts, InRiverDestination.Parts);

            //Process Relationships for any Parts
            await ProcessDataUpdates(unprocessedParts, InRiverDestination.StockPartRelationship);

            return sr;
        }
        #endregion

        #region BuildWorkbook
        private static async Task<XLWorkbook> BuildWorkbook(List<CommonLookup> headerfields, SqlDataReader reader, SqlConnection connection, string filename)
        {
            Console.WriteLine("BuildWorkbook");
            //now let's build a spreadsheet
            XLWorkbook workbook=null;

            var t = Task.Run( () =>
            {
                workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Sheet1");

                int row = 1;
                int idx = 1;
                foreach (CommonLookup cl in headerfields)
                {
                    worksheet.Row(row).Cell(idx).SetDataType(XLCellValues.Text);
                    worksheet.Row(row).Cell(idx).SetValue(cl.Code);
                    idx++;
                }

                row = row + 1;
                idx = 1;
                while (reader.Read() &&  row<100000)
                {
                    foreach (CommonLookup cl in headerfields)
                    {
                        worksheet.Row(row).Cell(idx).SetDataType(XLCellValues.Text);
                        worksheet.Row(row).Cell(idx).SetValue(reader[cl.CodeDesc].ToString());
                        idx++;
                    }
                    row++;
                    idx = 1;
                }

                workbook.SaveAs(filename);
            });

            t.Wait();

            Console.WriteLine("Finished - BuildingWorkbook");
            return workbook;
        }
        #endregion

        #region CheckSQLForItems
        private static async Task<InRiverServiceResult> CheckSQLForItems(IEnumerable<InRiverProcessorItem> items, InRiverDestination dest)
        {
            InRiverServiceResult sr = new InRiverServiceResult();

            IEnumerable<Task> tasks = from InRiverProcessorItem in items select CheckItem(InRiverProcessorItem,dest);

            await Task.WhenAll(tasks);

            return sr;
        }
        private static async Task CheckItem(InRiverProcessorItem item, InRiverDestination dest)
        {
            string sql="";

            switch (dest)
            {
                case InRiverDestination.Products:
                    sql = String.Format("Select count(StockNo) from ProductMaster where StockNo='{0}'", item.StockNoOrPart);
                    break;
                case InRiverDestination.Parts:
                    sql = String.Format("Select count(Part) from Part where CompanyId={0} and Part='{1}'", item.CompanyID, item.StockNoOrPart);
                    break;
            }

            using (SqlConnection connection = new SqlConnection(APIModels.Data.ConnectionManager.ProductDataHubConnectionString))
            {
                connection.Open();

                SqlCommand cmd = new SqlCommand(sql, connection);

                object result = await cmd.ExecuteScalarAsync();

                if (result!=null)
                {
                    int cnt = 0;
                    int.TryParse(result.ToString(), out cnt);

                    if (cnt==0)
                    {
                        await Task.Run(() =>
                        {
                            //pull from Unidata to SQL
                            if (dest == InRiverDestination.Products)
                            {
                                Console.WriteLine("Adding Product " + item.StockNoOrPart);

                                ProductMaster pm = new ProductMaster(item.StockNoOrPart);

                                pm.Save(APIModels.Data.ConnectionManager.ProductDataHubConnectionString);                               
                            }
                            else
                            {
                                Console.WriteLine("Adding Part " + item.StockNoOrPart);

                                SellingPart sp = new SellingPart(item.CompanyID, item.StockNoOrPart);

                                sp.SaveSQL(APIModels.Data.ConnectionManager.ProductDataHubConnectionString);
                            }
                        });
                    }
                }
            }

            return;
        }
        #endregion

        #region GetEntityProductTemplateData
        private static async Task<SqlDataReader> GetEntityProductTemplateData(SqlConnection connection, IEnumerable<InRiverProcessorItem> items)
        {
            if (!items.Any()) { return null; }
            SqlDataReader reader;

            string view = "select * from vw_InRiverProductTemplate";

            if (items!=null)
            {
                string[] stocknos = items.Select(o => "'"+o.StockNoOrPart+"', ").ToArray();
                view = view + " Where ProductStockNumber in (";
                var w = string.Concat(stocknos);
                w = w.Substring(0, w.Length - 2);
                view = view + w;
                view = view + ") ";
            }           

            SqlCommand cmd = new SqlCommand(view, connection);

            reader=await cmd.ExecuteReaderAsync();
           
            return reader;
        }
        #endregion

        #region GetEntityItemTemplateData
        private static async Task<SqlDataReader> GetEntityItemTemplateData(SqlConnection connection, IEnumerable<InRiverProcessorItem> items)
        {
            SqlDataReader reader;

            string view = "select * from vw_InRiverItemTemplate";

            if (items != null && items.Any())
            {
                string[] parts = items.Select(o => "'" + o.StockNoOrPart + "', ").ToArray();
                view = view + " Where ItemPart in (";
                var w = string.Concat(parts);
                w = w.Substring(0, w.Length - 2);
                view = view + w;
                view = view + ") ";
            }

            SqlCommand cmd = new SqlCommand(view, connection);

            reader = await cmd.ExecuteReaderAsync();

            return reader;
        }
        #endregion

        #region GetEntityRelationshipData
        private static async Task<SqlDataReader> GetEntityRelationshipTemplateData(SqlConnection connection, IEnumerable<InRiverProcessorItem> items)
        {
            SqlDataReader reader;

            string view = "select * from vw_InRiverRelationshipTemplate";

            if (items != null && items.Any())
            {
                string[] parts = items.Select(o => "'" + o.CompanyID.ToString().PadLeft(3,'0')+"!"+o.StockNoOrPart + "', ").ToArray();
                view = view + " Where UnidataKey in (";
                var w = string.Concat(parts);
                w = w.Substring(0, w.Length - 2);
                view = view + w;
                view = view + ") ";
            }

            SqlCommand cmd = new SqlCommand(view, connection);

            reader = await cmd.ExecuteReaderAsync();

            return reader;
        }
        #endregion

        #region GetEntityItemTemplateData
        private static async Task<SqlDataReader> GetEntityData(SqlConnection connection, IEnumerable<InRiverProcessorItem> items, InRiverDestination dest)
        {
            SqlDataReader reader;

            string view = "select * from vw_InRiverItemTemplate";

            if (items != null)
            {
                string[] parts = items.Select(o => "'" + o.StockNoOrPart + "',").ToArray();
                view = view + " Where Part in (";
                var w = string.Concat(parts);
                w = w.Substring(0, w.Length - 2);
                view = view + w;
                view = view + ") ";
            }

            SqlCommand cmd = new SqlCommand(view, connection);

            reader = await cmd.ExecuteReaderAsync();

            return reader;
        }
        #endregion

        #region ProcessDataUpdates
        private static async Task<InRiverServiceResult> ProcessDataUpdates(IEnumerable<InRiverProcessorItem> items, InRiverDestination dest)
        {
            InRiverServiceResult sr = new InRiverServiceResult();

            if (items == null || !items.Any()) { return sr; }

            string filename = Guid.NewGuid().ToString() + ".xlsx";

            string template=PRODUCT_TEMPLATE;
            
            //set defaults based on dest type
            switch (dest)
            {
                case InRiverDestination.Products:
                    sr.ProcessTask="InRiver.ProcessStockNoUpdates";
                    template=PRODUCT_TEMPLATE;
                    break;

                case InRiverDestination.Parts:
                    sr.ProcessTask="InRiver.ProcessPartsUpdates";
                    template=ITEM_TEMPLATE;
                    break;

                case InRiverDestination.StockPartRelationship:
                    sr.ProcessTask = "InRiver.ProcessRelationships";
                    template = RELATIONSHIP_TEMPLATE;
                    break;

                default:
                    sr.ProcessTask="InRiver.ProcessStockNoUpdates";
                    template=PRODUCT_TEMPLATE;
                    break;
            }

            //Get headers and related data fields            
            List<CommonLookup> headerfields = Task.Run(() => CommonLookup.GetLookups(template, true)).Result;

            using (SqlConnection connection = new SqlConnection(APIModels.Data.ConnectionManager.ProductDataHubConnectionString))
            {
                connection.Open();

                Object readerObject=null;

                if (dest==InRiverDestination.Products)
                {
                    readerObject = await GetEntityProductTemplateData(connection, items);
                }

                if (dest==InRiverDestination.Parts)
                {
                    readerObject = await GetEntityItemTemplateData(connection, items);
                }

                if (dest==InRiverDestination.StockPartRelationship)
                {
                    readerObject = await GetEntityRelationshipTemplateData(connection, items);
                }

                if (readerObject!=null)
                {
                    SqlDataReader reader = (SqlDataReader)readerObject;

                    XLWorkbook workbook = null;

                    workbook = await BuildWorkbook(headerfields, reader, connection, filename);

                    reader.Close();

                    Boolean fileCopied = await CopyToDestination(filename, dest);

                    if (fileCopied)
                    {
                        await UpdateState(items, InRiverState.Complete);
                        sr.LastProcessed = sr.Processed;
                        sr.FileName = filename;
                        sr.LogDate = DateTime.Now;
                        sr.ProcessStatus = "Finished";
                        await UpdateState(items, InRiverState.Complete);
                    }
                    else
                    {
                        sr.FileName = filename;
                        sr.LogDate = DateTime.Now;
                        sr.ProcessStatus = "Failure";
                        sr.ErrorMessage = "File Not Copied";
                        await UpdateState(items, InRiverState.Failure);
                    }

                    sr.Save(connection);
                }

            }

            return sr;

        }
        #endregion 

        #region ProcessPartUpdates
        private static async Task<InRiverServiceResult> ProcessPartUpdates(IEnumerable<InRiverProcessorItem> items)
        {
            InRiverServiceResult sr = new InRiverServiceResult();

            string filename = Guid.NewGuid().ToString() + ".xlsx";

            sr.ProcessTask = "InRiver.ProcessPartsUpdates";

            //Get headers and related data fields            
            List<CommonLookup> headerfields = Task.Run(() => CommonLookup.GetLookups(ITEM_TEMPLATE, true)).Result;

            using (SqlConnection connection = new SqlConnection(APIModels.Data.ConnectionManager.ProductDataHubConnectionString))
            {
                connection.Open();

                SqlDataReader reader = await GetEntityItemTemplateData(connection, null);

                XLWorkbook workbook = null;

                workbook = await BuildWorkbook(headerfields, reader, connection, filename);

                reader.Close();

                Boolean fileCopied=await CopyToDestination(filename, InRiverDestination.Parts);

                if (fileCopied)
                {
                    await UpdateState(items, InRiverState.Complete);
                    sr.LastProcessed = sr.Processed;
                    sr.FileName = filename;
                    sr.LogDate = DateTime.Now;
                }

                sr.Save(connection);

            }

            return sr;
        }
        #endregion

        #region ProcessProductsAll
        public static async Task<InRiverServiceResult> ProcessProductsAll()
        {
            //only handles stock numbers

            string filename=Guid.NewGuid().ToString()+".xlsx";
            
            InRiverServiceResult sr = new InRiverServiceResult();

            sr.ProcessTask = "InRiver.ProcessProductsAll";

            //Get headers and related data fields            
            List<CommonLookup> headerfields = Task.Run(()=>CommonLookup.GetLookups(PRODUCT_TEMPLATE, true)).Result;

            //get data from view dbo.usp_InRiverProductTemplate
            

            using (SqlConnection connection= new SqlConnection(APIModels.Data.ConnectionManager.ProductDataHubConnectionString))
            {
                connection.Open();

                SqlDataReader reader = await GetEntityProductTemplateData(connection,null);

                XLWorkbook workbook = null;

                workbook = await BuildWorkbook(headerfields, reader, connection, filename);

                reader.Close();

                Boolean fileCopied = await CopyToDestination(filename, InRiverDestination.Products);

                if (fileCopied)
                {
                    sr.LastProcessed = sr.Processed;
                    sr.FileName = filename;
                    sr.LogDate = DateTime.Now;
                }

                sr.Save(connection);

            }

            return sr;
        }
        #endregion

        #region ProcessStockNumberUpdates
        private static async Task<InRiverServiceResult> ProcessStockNumberUpdates(IEnumerable<InRiverProcessorItem> items)
        {
            InRiverServiceResult sr = new InRiverServiceResult();

            string filename = Guid.NewGuid().ToString() + ".xlsx";

            //Get headers and related data fields
            List<CommonLookup> headerfields = CommonLookup.GetLookups(PRODUCT_TEMPLATE, true);

            //get data from view dbo.usp_InRiverProductTemplate
            XLWorkbook workbook = null;
            using (SqlConnection connection = new SqlConnection(APIModels.Data.ConnectionManager.ProductDataHubConnectionString))
            {
                SqlDataReader reader = await GetEntityProductTemplateData(connection, items);

                workbook = await BuildWorkbook(headerfields, reader, connection, filename);

                reader.Close();

                Boolean fileCopied = await CopyToDestination(filename, InRiverDestination.Products);

                if (fileCopied)
                {
                    await UpdateState(items, InRiverState.Complete);
                    sr.LastProcessed = sr.Processed;
                    sr.FileName = filename;
                    sr.LogDate = DateTime.Now;
                }

                sr.Save(connection);
            }


            await UpdateState(items, InRiverState.Complete);

            return sr;
        }
        #endregion

    }
}
