using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIModels;
using APIModels.InRiver;
using System.Net;
using System.Configuration;
namespace InRiverService
{
    class Program
    {
        static void Main(string[] args)
        {
            AddSettings();

            RunAsync(args);

            return;
        }

        static void ShowHelp()
        {
            //from http://patorjk.com/software/taag/#p=display&f=Doom&t=InRiverService
            Console.Clear();
            Console.WriteLine(@"
 _____     ______ _                _____                 _          
|_   _|    | ___ (_)              /  ___|               (_)         
  | | _ __ | |_/ /___   _____ _ __\ `--.  ___ _ ____   ___  ___ ___ 
  | || '_ \|    /| \ \ / / _ \ '__|`--. \/ _ \ '__\ \ / / |/ __/ _ \
 _| || | | | |\ \| |\ V /  __/ |  /\__/ /  __/ |   \ V /| | (_|  __/
 \___/_| |_\_| \_|_| \_/ \___|_|  \____/ \___|_|    \_/ |_|\___\___|");
            Console.WriteLine("");
            Console.WriteLine("Options");
            Console.WriteLine("________________________________________________________");               
            Console.WriteLine("nothing specified processes Products and Parts UPDATES");
            Console.WriteLine("________________________________________________________");
            Console.WriteLine("Products=ALL");
            Console.WriteLine("Products=UPDATE");
            Console.WriteLine("Parts=ALL");
            Console.WriteLine("Parts=UPDATE");



        }
        static void RunAsync(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                var sr = Updates().Result;
                return;
            }

            InRiverServiceOptions opts = new InRiverServiceOptions(args);

            //Show Help
            if (opts.ShowHelp) { ShowHelp(); return; }



            if (opts.Destination==InRiverDestination.Products)
            {
                if (opts.DataOption==InRiverDataOptions.Updates)
                {

                }
            }
           

            

            return;
        }

        public static async  Task<InRiverServiceResult> ProductsFull()
        {
            Console.WriteLine("Running --  Process Products All");
            
            var sr = await InRiverProcessor.ProcessUpdates();

            Console.WriteLine("Complete -- Process Products All");

            return sr;
        }

        public static async Task<InRiverServiceResult> Updates()
        {
            Console.WriteLine("Running --  Updates All");

            var sr = await InRiverProcessor.ProcessUpdates();

            Console.WriteLine("Complete -- Updates All");

            return sr;
        }


        private static void AddSettings()
        {
            WebClient wc = new WebClient();
            string url = ConfigurationManager.AppSettings.Get("settingsurl");
            ConfigurationManager.AppSettings.Set("MongoServer", wc.DownloadString(url + "/setting/svcapi_mongoserver"));
            ConfigurationManager.AppSettings.Set("MongoPort", wc.DownloadString(url + "/setting/svcapi_mongoport"));
            ConfigurationManager.AppSettings.Set("Conn", wc.DownloadString(url + "/setting/svcapi_conn"));
            ConfigurationManager.AppSettings.Set("DatahubConn", wc.DownloadString(url + "/setting/svcapi_datahubconn"));
            ConfigurationManager.AppSettings.Set("conn", wc.DownloadString(url + "/setting/svcapi_conn"));
            ConfigurationManager.AppSettings.Set("Conn9", wc.DownloadString(url + "/setting/svcapi_productdatahubconn"));

            ConfigurationManager.AppSettings.Set("udtype", wc.DownloadString(url + "/setting/udtype"));
            string tmp = wc.DownloadString(url + "/setting/udhost");
            ConfigurationManager.AppSettings.Set("udhost", tmp);
            ConfigurationManager.AppSettings.Set("HOST", tmp);

            ConfigurationManager.AppSettings.Set("ACCOUNT1", wc.DownloadString(url + "/setting/ACCOUNT1"));
            ConfigurationManager.AppSettings.Set("ACCOUNT3", wc.DownloadString(url + "/setting/ACCOUNT3"));
            ConfigurationManager.AppSettings.Set("ACCOUNT5", wc.DownloadString(url + "/setting/ACCOUNT5"));
            ConfigurationManager.AppSettings.Set("ACCOUNT6", wc.DownloadString(url + "/setting/ACCOUNT6"));
            ConfigurationManager.AppSettings.Set("ACCOUNT9", wc.DownloadString(url + "/setting/ACCOUNT9"));

            tmp = wc.DownloadString(url + "/setting/svcapi_uduser");
            ConfigurationManager.AppSettings.Set("uduser", tmp);
            ConfigurationManager.AppSettings.Set("ID", tmp);

            tmp = wc.DownloadString(url + "/setting/svcapi_udpwd");
            ConfigurationManager.AppSettings.Set("udpwd", tmp);
            ConfigurationManager.AppSettings.Set("PWD", tmp);
        }
    }
}
