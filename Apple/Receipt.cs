using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
//using Newtonsoft.Json.Linq;

namespace Framework.Caspar.Apple
{
    static public class Receipt
    {
        public class Result
        {
            [Serializable]
            public class InApp
            {
                public string original_transaction_id;
                public string transaction_id;
                public string product_id;
                public string purchase_date;
                public string original_purchase_date;
                public string unique_identifier;
            }

            [Serializable]
            public class Receipt
            {
                public List<InApp> in_app;
            }

            public Receipt receipt;
            public int status;

        }

        public static async Task<Result> Verify(string receiptData, bool sandbox = false)
        {

            for (int i = 0; i < 3; ++i)
            {
                try
                {
                    // Verify the receipt with Apple
                    string postString = String.Format("{{ \"receipt-data\" : \"{0}\" }}", receiptData);
                    byte[] postBytes = Encoding.UTF8.GetBytes(postString);
                    HttpWebRequest request;

                    if (sandbox == true)
                    {
                        request = WebRequest.Create("https://sandbox.itunes.apple.com/verifyReceipt") as HttpWebRequest;
                    }
                    else
                    {
                        request = WebRequest.Create("https://buy.itunes.apple.com/verifyReceipt") as HttpWebRequest;
                    }


                    request.Method = "POST";
                    request.ContentType = "text/plain";
                    request.ContentLength = postBytes.Length;
                    using (Stream postStream = request.GetRequestStream())
                    {
                        await postStream.WriteAsync(postBytes, 0, postBytes.Length);
                        postStream.Close();
                    }


                    Result result = null;

                    using (WebResponse r = await request.GetResponseAsync())
                    {
                        using (System.IO.StreamReader sr = new System.IO.StreamReader(r.GetResponseStream()))
                        {
                            var data = sr.ReadToEnd();
                            result = Newtonsoft.Json.JsonConvert.DeserializeObject<Framework.Caspar.Apple.Receipt.Result>(data);
                        }
                    }

                    if (result.status == 21007 && sandbox == false)
                    {
                        return await Verify(receiptData, true);
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    global::Framework.Caspar.Api.Logger.Debug(ex);
                }
            }

            return new Result() { status = 1 };

        }
    }
}
