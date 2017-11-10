namespace ReceiveAttachmentBot
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;
    using Microsoft.WindowsAzure.Storage; // Namespace for CloudStorageAccount
    using Microsoft.WindowsAzure.Storage.Blob; // Namespace for Blob storage types
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Web;
    using System.Runtime.Serialization;
    using System.Collections.Generic;
    using System.IO;
    using System.Drawing;

    [Serializable]
    internal class ReceiveAttachmentDialog : IDialog<object>
    {
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(this.MessageReceivedAsync);
        }

        private static Attachment GetInternetAttachment(string text)
        {
            var imagePath = HttpContext.Current.Server.MapPath("~/images/G1-3.jpg");
            Image tmpImage = Image.FromFile(imagePath);

            using (Graphics gfx = System.Drawing.Graphics.FromImage(tmpImage))
            {

                SolidBrush mybrush = new SolidBrush(Color.Red);
                Font myfont = new Font("標楷體", 12);
                gfx.FillRectangle(new SolidBrush(Color.White), 70, 130, 130, 80);
                gfx.DrawString(text , myfont, mybrush, new Rectangle(70, 135, 130, 80));
            }

            MemoryStream msImg = new MemoryStream();
            tmpImage.Save(msImg, System.Drawing.Imaging.ImageFormat.Jpeg);

            var imageData = Convert.ToBase64String(msImg.ToArray());

            //var imageData = Convert.ToBase64String(File.ReadAllBytes(imagePath));

            return new Attachment
            {
                Name = "G1-3.jpg",
                ContentType = "image/jpg",
                ContentUrl = $"data:image/jpg;base64,{imageData}"
            };

            //return new Attachment
            //{
            //    Name = "BotFrameworkOverview.jpg",
            //    ContentType = "image/jpg",
            //    ContentUrl = "https://i10.hoopchina.com.cn/hupuapp/bbs/568/31656568/thread_31656568_20171008155221_s_55795_h_750px_w_750px1961934395.jpeg?x-oss-process=image/resize,w_800/format,jpeg"
            //};
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var message = await argument;

            if (message.Attachments != null && message.Attachments.Any())
            {
                var attachment = message.Attachments.First();
                using (HttpClient httpClient = new HttpClient())
                {
                    // Skype & MS Teams attachment URLs are secured by a JwtToken, so we need to pass the token from our bot.
                    if ((message.ChannelId.Equals("skype", StringComparison.InvariantCultureIgnoreCase) || message.ChannelId.Equals("msteams", StringComparison.InvariantCultureIgnoreCase)) 
                        && new Uri(attachment.ContentUrl).Host.EndsWith("skype.com"))
                    {
                        var token = await new MicrosoftAppCredentials().GetTokenAsync();
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    }

                    var responseMessage = await httpClient.GetAsync(attachment.ContentUrl);

                    var contentLenghtBytes = responseMessage.Content.Headers.ContentLength;

                    //[John] Replaced with cognitive service.
                    //await context.PostAsync($"Attachment of {attachment.ContentType} type and size of {contentLenghtBytes} bytes received.");

                    //Connect to Cognitive Services
                    string subscriptionKey = "[Your subscription key of cognitive services api]";
                    string uriBase = "https://southeastasia.api.cognitive.microsoft.com/vision/v1.0/analyze";
                    HttpClient client = new HttpClient();
                    // Request headers.
                    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                    // Request parameters. A third optional parameter is "details".
                    string requestParameters = "visualFeatures=Categories,Description,Color&language=en";

                    // Assemble the URI for the REST API Call.
                    string uri = uriBase + "?" + requestParameters;
                    HttpResponseMessage response;

                    byte[] byteData = await responseMessage.Content.ReadAsByteArrayAsync();
                    using (ByteArrayContent content = new ByteArrayContent(byteData))
                    {
                        // This example uses content type "application/octet-stream".
                        // The other content types you can use are "application/json" and "multipart/form-data".
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                        // Execute the REST API call.
                        response = await client.PostAsync(uri, content);

                        // Get the JSON response.
                        string contentString = await response.Content.ReadAsStringAsync();

                        // Get discription of the image
                        dynamic jsonData = JObject.Parse(contentString);
                        string data = jsonData["description"]["captions"][0]["text"].ToString();
                        string msg = "I saw: " + data;

                        //[John] Translation
                        string from = "en";
                        string to = "zh";
                        uri = "https://api.microsofttranslator.com/v2/Http.svc/Translate?text=" + HttpUtility.UrlEncode(msg) + "&from=" + from + "&to=" + to;
                        //uri = "https://api.cognitive.microsoft.com/sts/v1.0?text=" + HttpUtility.UrlEncode(msg) + "&from=" + from + "&to=" + to;

                        HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                        httpWebRequest.Headers.Add("Ocp-Apim-Subscription-Key", "[Your subscription key of translator api]");
                        using (WebResponse responseTrans = httpWebRequest.GetResponse())
                        using (System.IO.Stream stream = responseTrans.GetResponseStream())
                        {
                            DataContractSerializer dcs = new DataContractSerializer(Type.GetType("System.String"));
                            string translation = (string)dcs.ReadObject(stream);
                            if (translation.Contains("女人")) translation = "肥宅我不是你老婆!";

                                var replyMessage = context.MakeMessage();

                                Attachment replyAtt = null;

                                replyAtt = GetInternetAttachment(translation);
                             

                                // The Attachments property allows you to send and receive images and other content
                                replyMessage.Attachments = new List<Attachment> { replyAtt };

                                await context.PostAsync(replyMessage);
                            
                        }

                    }

                }
            }
            else
            {
                await context.PostAsync("Hi, 我是結衣, 我會告訴你我看到了甚麼. 傳一張照片上來吧!");
            }

            context.Wait(this.MessageReceivedAsync);
        }
    }
}