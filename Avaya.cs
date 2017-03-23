using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIModels.AvayaTSWS;
using System.Configuration;
using System.Xml.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Web;
using System.Net;
using Dapper;
using System.Data;
using System.Data.SqlClient;
namespace APIModels.Avaya
{
    public class AvayaResult
    {
        public string Extension {get;set;}
        public string Destination { get; set; }
        public Exception Error { get; set; }
    }
    public class Avaya
    {
        public static string AVAYA_USERKEY="AvayaTSWSUser";
        public static string AVAYA_PWDKEY = "AvayaTSWSPWD";
        public static string AVAYA_URL = "AvayaTSWSUrl";
        public static string AVAYA_OUTDIAL="9";
        public static string DEFAULT_COMPANY="9";

        #region MakeCall
        public static AvayaResult MakeCallByUser(string user, string dest)
        {
            //get user ext based on user
            //we need a cross-reference between the user and the ext
            //possible solution is BHCCRM..SecurityUsers

            string callFrom="";
            string callTo="";

            callFrom = APIModels.Security.User.GetUserExtension(user);

            if (string.IsNullOrEmpty(callFrom)) { return new AvayaResult { Error = new Exception("Unable to determine Phone Extension for " + user + ".") }; };

            return MakeCall(callFrom, callTo);
        }

        public static AvayaResult MakeCall(string sourceExt, string dest)
        {
            AvayaResult result = new AvayaResult { Extension = sourceExt, Destination = dest };

            if (String.IsNullOrEmpty(sourceExt) || String.IsNullOrEmpty(dest)) { result.Error = new Exception("Invalid or Missing Parameters"); return result; }

            Int32 e = 0;

            if (!Int32.TryParse(sourceExt, out e)) { return MakeCallByUser(sourceExt, dest); }

            // take the incoming number and add any prefixes to it for out-dial
            dest = PrefixDestination(sourceExt, dest);

            AppUsage.Log("Avaya", DEFAULT_COMPANY, sourceExt, "MakeCall", dest);

            //Establish Service
            AvayaTSWS.TelephonyServiceService svc=new AvayaTSWS.TelephonyServiceService();
            svc.Url = (string)Cache.MemoryCacher.GetValue(AVAYA_URL)??CommonLookup.GetLookups("BHCCRM",AVAYA_URL,true)[0].CodeDesc; // "http://192.168.101.26/axis/services/TelephonyService";

            Cache.MemoryCacher.Add(AVAYA_URL, svc.Url);

            //endpoints
            AvayaTSWS.endpoints ep = new AvayaTSWS.endpoints();            
            ep.originatingExtension = sourceExt; // System.Configuration.ConfigurationManager.AppSettings.Get("ext").ToString();
            ep.destinationNumber = dest;

            try
            {
                //do work
                System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) { return true; };

                string sess = null;

                svc.attach(sess);

                svc.makeCall(ep);
            }
            catch (Exception ex)
            {
                result.Error = ex;
            }

            return result; 
        }



        #endregion

        #region Prefix
        public static string PrefixDestination(string ext,string dest)
        {
            //use the ext and dest to determine any prefixes that should be applied to the phone number
            int e = 0;

            int.TryParse(ext, out e);

            if (e>=3000 && e<=5750)
            {
                //Charlotte
                if (dest.Substring(0,3)=="704" || dest.Substring(0,3)=="980")
                {
                    return AVAYA_OUTDIAL + dest;
                }
                return AVAYA_OUTDIAL + "1" + dest;
            }
            else
            {
                if (e>=6500 && e<=7000)
                {
                    //Heiser/Miami
                    if (dest.Substring(0,3)=="585")
                    {
                        return AVAYA_OUTDIAL + dest.Substring(3);
                    }
                    if (dest.Substring(0,3)=="704")
                    {
                        return AVAYA_OUTDIAL + dest;
                    }
                    return AVAYA_OUTDIAL + "1" + dest;
                    
                }
            }

            return dest;
        }
        #endregion
    }
}


//custom override of the Reference.cs for the AvayaTSWS so that we can add the appropriate user information
namespace APIModels.AvayaTSWS
{
    public partial class TelephonyServiceService : System.Web.Services.Protocols.SoapHttpClientProtocol
    {
        #region GetWebRequest
        protected override System.Net.WebRequest GetWebRequest(Uri uri)
        {
            
            string username = (string)Cache.MemoryCacher.GetValue(Avaya.Avaya.AVAYA_USERKEY)??CommonLookup.GetLookups("BHCCRM",Avaya.Avaya.AVAYA_USERKEY, true)[0].CodeDesc;  //ConfigurationManager.AppSettings.Get("user").ToString();

            string pwd = (string)Cache.MemoryCacher.GetValue(Avaya.Avaya.AVAYA_PWDKEY)??CommonLookup.GetLookups("BHCCRM",Avaya.Avaya.AVAYA_PWDKEY, true)[0].CodeDesc;       //ConfigurationManager.AppSettings.Get("pwd").ToString();

            Cache.MemoryCacher.Add(Avaya.Avaya.AVAYA_USERKEY, username);
            
            Cache.MemoryCacher.Add(Avaya.Avaya.AVAYA_PWDKEY, pwd);

            System.Net.WebRequest request = base.GetWebRequest(uri);

            //request.Headers.Add("username", "avayacce@swlink1");

            //request.Headers.Add("password", "passwordhere");

            request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(username + ":" + pwd));

            return request;
        }
        #endregion
    }
}
