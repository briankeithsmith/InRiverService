using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using APIModels;
using Dapper;
namespace APIModels.InRiver
{
    public enum InRiverState
    {
        None=0,
        InProcess=1,
        Complete=3,
        Failure=9
    }

    public enum InRiverDestination
    {
        Products,
        Parts,
        StockPartRelationship,
        All
    }

    public class InRiverProcessorItem
    {
        public long ID { get; set; }

        public int CompanyID { get; set; }

        public string StockNoOrPart { get; set; }

        public DateTime DateChanged { get; set; }

        public DateTime InRiverStartDate { get; set; }

        private InRiverState _inRiverState;

        public InRiverState InRiverState { 
            get { return _inRiverState; }
            set
            {
                _inRiverState = value;

                InRiverStartDate = DateTime.Now;

                InRiverStateDate = DateTime.Now;

                if (_inRiverState == InRiverState.Complete) { InRiverCompletedDate = InRiverStateDate; }
            }
        
        }


        public DateTime? InRiverStateDate { get; set; }

        public DateTime? InRiverCompletedDate
        {
            get;
            set;
        }

        public async Task<long> SaveAsync()
        {
            using (SqlConnection connection=new SqlConnection(APIModels.Data.ConnectionManager.ProductDataHubConnectionString))
            {
                connection.Open();

                string sql = "Update InRiverSync set InRiverState=@InRiverState , InRiverStateDate=@InRiverStateDate, InRiverCompletedDate=@InRiverCompletedDate where ID=@ID";
                
                await connection.ExecuteAsync(sql, this);
            }
            return ID;
        }

        public static async Task<IEnumerable<InRiverProcessorItem>> GetUnprocessed(InRiverState state = InRiverState.None)
        {
            int st = (int)state;

            string sql = "Select * from InRiverSync where InRiverState="+st.ToString();

            IEnumerable<InRiverProcessorItem> items;

            using (SqlConnection connection = new SqlConnection(APIModels.Data.ConnectionManager.ProductDataHubConnectionString))
            {
                await connection.OpenAsync();

                items=await connection.QueryAsync<InRiverProcessorItem>(sql);
            }

            return items;
        }

        #region PushItemForUpdate
        public static async Task<long> PushItemForUpdate(string companyid, string item)
        {
            int cid = 0;
            int.TryParse(companyid, out cid);
            long result = await PushItemForUpdate(cid, item);
            return result;
        }

        public static async Task<long> PushItemForUpdate(int companyid, string item)
        {
            object result = 0;
            //item can be a stock no or a selling part no
            //companyid determines whether it is a stock no or selling part no
            using (SqlConnection conn = new SqlConnection(APIModels.Data.ConnectionManager.ProductDataHubConnectionString))
            {
                await conn.OpenAsync();

                string sql = "Insert into InRiverSync ( CompanyID, StockNoOrPart, DateChanged, InRiverState, InRiverStartDate) values (" + companyid.ToString() + ", '" + item + "', getdate(), 0, getdate()); Select @@Identity as ID ";

                SqlCommand cmd = new SqlCommand(sql, conn);

                result = await cmd.ExecuteScalarAsync();
            }
            long id = 0;

            long.TryParse(result.ToString(), out id);

            return id;
        }
        #endregion

    }
}
