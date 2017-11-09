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

    [Serializable]
    internal class ReceiveAttachmentDialog : IDialog<object>
    {
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(this.MessageReceivedAsync);
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

                    //Connect to Cognitive Services
                    string subscriptionKey = "[Your cognitive service subscription key]";
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

                        //Translation API
                        string from = "en";
                        string to = "zh";
                        uri = "https://api.microsofttranslator.com/v2/Http.svc/Translate?text=" + HttpUtility.UrlEncode(msg) + "&from=" + from + "&to=" + to;
                        //uri = "https://api.cognitive.microsoft.com/sts/v1.0?text=" + HttpUtility.UrlEncode(msg) + "&from=" + from + "&to=" + to;

                        HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                        httpWebRequest.Headers.Add("Ocp-Apim-Subscription-Key", "[Your translator api key]");
                        using (WebResponse responseTrans = httpWebRequest.GetResponse())
                        using (System.IO.Stream stream = responseTrans.GetResponseStream())
                        {
                            DataContractSerializer dcs = new DataContractSerializer(Type.GetType("System.String"));
                            string translation = (string)dcs.ReadObject(stream);

                            await context.PostAsync(translation);
                        }

                    }

                }
            }
            else
            {
                await context.PostAsync("Hi there! I'm a bot created to show you how I can receive message attachments, but no attachment was sent to me. Please, try again sending a new message including an attachment.");
            }

            context.Wait(this.MessageReceivedAsync);
        }
    }
}