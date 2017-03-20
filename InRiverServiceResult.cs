using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIModels.InRiver
{
    public class InRiverServiceResult : APIModels.CFPStatus
    {
        public int Records { get { return Processed; } set { Processed = value; } }

        public List<InRiverProcessorItem> ErrorItems { get; set; }
        
        public InRiverServiceResult():base()
        {
            ErrorItems = new List<InRiverProcessorItem>();
        }
    }
}
