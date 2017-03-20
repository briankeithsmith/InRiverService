using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIModels.InRiver
{
    public enum InRiverDataOptions
    {
        All=0,
        Updates=1
    }
    public class InRiverServiceOptions
    {
        public InRiverDestination Destination { get; set; }
        public InRiverDataOptions DataOption { get; set; }
        public Boolean ShowHelp { get; set; }

        public InRiverServiceOptions(string[] args)
        {
            Destination = InRiverDestination.All;
            DataOption = InRiverDataOptions.All;
            ShowHelp = false;
            ParseArgs(args);
        }
        
        private void ParseArgs(string[] args)
        {


            foreach (string a in args)
            {
                //only care about the first option
                if (!String.IsNullOrEmpty(a))
                {
                    string[] work = a.Split('=');

                    if (work.Length==1)
                    {
                        if (work[0].ToLower() == "help") { ShowHelp = true; return; }
                        if (work[0].ToLower() == "products") { Destination = InRiverDestination.Products; }
                        if (work[0].ToLower() == "parts") { Destination = InRiverDestination.Parts; }
                    }

                    if (work.Length>1)
                    {
                        if (work[1].ToLower() == "all") { DataOption = InRiverDataOptions.All; return;}
                        if (work[1].ToLower() == "update") { DataOption = InRiverDataOptions.Updates; return; }
                    }
                    return;                   
                }
            }
        }
    }
}
