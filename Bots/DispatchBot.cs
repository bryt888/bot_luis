// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.CognitiveServices.Language.LUIS;

namespace Microsoft.BotBuilderSamples
{
    public class DispatchBot : ActivityHandler
    {
        private ILogger<DispatchBot> _logger;
        private IBotServices _botServices;
        //stae management
        private BotState _conversationState;
        private BotState _userState;
        private string _luis_topintent = "";
        private List<luisTime> _luis_dates = new List<luisTime>();
        private List<luisLocation> _luis_locs = new List<luisLocation>();
        private string _luis_query_text = "";

        public DispatchBot(IBotServices botServices, ILogger<DispatchBot> logger, ConversationState conversationState, UserState userState)
        {
            _logger = logger;
            _botServices = botServices;
            _conversationState = conversationState;
            _userState = userState;

            
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occured during the turn.
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            //get user's state
            var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());

            var userStateAccessors = _userState.CreateProperty<UserProfile>(nameof(UserProfile));
            var userProfile = await userStateAccessors.GetAsync(turnContext, () => new UserProfile());
            var userid = userProfile.userid;
            var homelocation = userProfile.homelocation;

            string sss = "1st";
            //check if the query text is more parameter the previous question needs
            if (conversationData.PromptedUserForLocation || conversationData.PromptedUserForTimePeriod)
            {
                sss = "2nd";
                string newQuery = conversationData.querystring + " " + turnContext.Activity.Text;
                turnContext.Activity.Text = newQuery;
                conversationData.PromptedUserForLocation = false;
            }
            

            // First, we use the dispatch model to determine which cognitive service (LUIS or QnA) to use.
            var recognizerResult = await _botServices.Dispatch.RecognizeAsync(turnContext, cancellationToken);
            /*
            JObject retAll = JObject.Parse(recognizerResult.Entities.ToString());
            var a1 = retAll.First.First;
            JObject a2 = retAll.GetValue("$instance") as JObject;
            JArray a3 = a2.GetValue("datetime") as JArray;
            var a4 = a3[0]["type"].ToString();
            var a5 = a3[0]["type"].ToString();
            JArray dts= retAll["datetime"] as JArray;
            foreach (var t in dts)
            {
                var cType = t["type"];
                var cTimex = t["timex"];
            }
            */
            // Top intent tell us which cognitive service to use.
            LuisResult luisResult = recognizerResult.Properties["luisResult"] as LuisResult;
            _luis_topintent = luisResult.TopScoringIntent.Intent;  //recognizerResult.GetTopScoringIntent();
            _luis_query_text = luisResult.Query;
            /*
            var retEntities = recognizerResult.Entities;
            int c = retEntities.Count;
            //Debug.WriteLine(topIntent.ToString());
            Debug.WriteLine(c);
            //Debug.WriteLine(retEntities.ToString());
            Debug.WriteLine(recognizerResult.Text.ToString());
            List<string> retTypes = new List<string>();

            */

            

            //await turnContext.SendActivityAsync(MessageFactory.Text($"query extra parameter {sss}."), cancellationToken);

            if (_luis_topintent == "Unknown" || _luis_topintent == "None") {
                
                _luis_topintent = "";
            }
            await turnContext.SendActivityAsync(MessageFactory.Text($"top intent {_luis_topintent}."), cancellationToken);

            //_luis_dates = luisResult.Entities.Where(i => i.Type == "builtin.datetimeV2.date").Select(i => i.Entity).ToList();

            //var aaa = luisResult.Entities.Select(i => i.AdditionalProperties["resolution"]).ToList();

            //var normalizedValue = luisResult.Entities[0].Resolution.Values.Select(s => ((List<object>)s)[0]).FirstOrDefault();

            foreach (var entity in luisResult.Entities)
            {
                if (entity.Type== "Location::City")
                {
                    luisLocation lloc = new luisLocation();
                    lloc.Type = "city";
                    lloc.City = entity.Entity;
                    _luis_locs.Add(lloc);
                   
                }
                else if (entity.Type == "builtin.datetimeV2.date" || entity.Type == "builtin.datetimeV2.daterange")
                {
                    luisTime lt = new luisTime();
                    JObject jEntity = JObject.Parse(entity.AdditionalProperties["resolution"].ToString());
                    JArray jValues = jEntity["values"] as JArray;
                    string strType = jValues[0]["type"].ToString().ToLower();
                    if (strType == "date")
                    {
                        lt.Type = "date";
                        lt.Timex = jValues[0]["timex"].ToString();
                        lt.StartTime = jValues[0]["value"].ToString();
                        lt.EndTime = null;
                    }
                    else if (strType == "daterange")
                    {
                        lt.Type = "daterange";
                        lt.Timex = jValues[0]["timex"].ToString();
                        lt.StartTime = jValues[0]["start"].ToString();
                        lt.EndTime = jValues[0]["end"].ToString();
                    }
                    _luis_dates.Add(lt);


                }

            }
            if (_luis_dates.Count>0 && _luis_locs.Count>0)
            {
                //both location and time are ok
                await turnContext.SendActivityAsync(MessageFactory.Text($"问题正常包含时间和地点。query变成：{luisResult.Query}"), cancellationToken);

            }
            else if (_luis_dates.Count > 0 && _luis_locs.Count == 0)
            {
                //no location
                conversationData.PromptedUserForLocation = true;
                conversationData.querystring = luisResult.Query;
                await turnContext.SendActivityAsync(MessageFactory.Text($"您的问题是：{luisResult.Query},请问您想知道那个城市的？"), cancellationToken);

            }
            else if (_luis_dates.Count == 0 && _luis_locs.Count > 0)
            {
                //no time
                //it's ok if asking current condition

            }else
            {
                //invalid query
                await turnContext.SendActivityAsync(MessageFactory.Text($"非正常请求！"), cancellationToken);

            }
 

            // Next, we call the dispatcher with the top intent.
            //await turnContext.SendActivityAsync(MessageFactory.Text($"ProcessWeather top intent {_luis_topintent}."), cancellationToken);
            //await turnContext.SendActivityAsync(MessageFactory.Text($"ProcessWeather Intents detected::\n\n{string.Join("\n\n", luisResult.Intents.Select(i => i.Intent))}"), cancellationToken);
            if (luisResult.Entities.Count > 0)
            {
                //await turnContext.SendActivityAsync(MessageFactory.Text($"ProcessWeather entities were found in the message:\n\n{string.Join("\n\n", luisResult.Entities.Select(i => i.Entity + "-->" + i.Type))}"), cancellationToken);


                //await turnContext.SendActivityAsync(MessageFactory.Text($"new test ******:\n\n{string.Join("\n\n", luisResult.Entities.Select(i => i.Entity + "-->" + i.AdditionalProperties["resolution"]))}"), cancellationToken);
            }
            //            await DispatchToTopIntentAsync(turnContext, "l_Weather", recognizerResult, cancellationToken);
            //await DispatchToTopIntentAsync(turnContext, topIntent.intent, recognizerResult, cancellationToken);
            string strChannels = turnContext.Activity.ChannelId + "******" + turnContext.Activity.Conversation.Id + "******" + turnContext.Activity.From.Id + "******" + turnContext.Activity.Recipient.Id;
            await turnContext.SendActivityAsync(MessageFactory.Text($"channel data:{strChannels}"), cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            const string WelcomeText = "Type a greeting, or a question about the weather to get started.";

            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Welcome to Dispatch bot {member.Name}. {WelcomeText}"), cancellationToken);
                }
            }
        }

        private async Task DispatchToTopIntentAsync(ITurnContext<IMessageActivity> turnContext, string intent, RecognizerResult recognizerResult, CancellationToken cancellationToken)
        {
            switch (intent)
            {
                case "l_HomeAutomation":
                    await ProcessHomeAutomationAsync(turnContext, recognizerResult.Properties["luisResult"] as LuisResult, cancellationToken);
                    break;
                case "l_Weather":
                    await ProcessWeatherAsync(turnContext, recognizerResult.Properties["luisResult"] as LuisResult, cancellationToken);
                    break;
                case "q_sample-qna":
                    await ProcessSampleQnAAsync(turnContext, cancellationToken);
                    break;
                default:
                    _logger.LogInformation($"Dispatch unrecognized intent: {intent}.");
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Dispatch unrecognized intent: {intent}."), cancellationToken);
                    break;
            }
        }

        
        private async Task ProcessHomeAutomationAsync(ITurnContext<IMessageActivity> turnContext, LuisResult luisResult, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProcessHomeAutomationAsync");

            // Retrieve LUIS result for Process Automation.
            var result = luisResult.ConnectedServiceResult;
            var topIntent = result.TopScoringIntent.Intent; 

            //for (string strType in result.Intents.)







            
            await turnContext.SendActivityAsync(MessageFactory.Text($"HomeAutomation top intent {topIntent}."), cancellationToken);
            await turnContext.SendActivityAsync(MessageFactory.Text($"HomeAutomation intents detected:\n\n{string.Join("\n\n", result.Intents.Select(i => i.Intent))}"), cancellationToken);
            if (luisResult.Entities.Count > 0)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"HomeAutomation entities were found in the message:\n\n{string.Join("\n\n", result.Entities.Select(i => i.Entity))}"), cancellationToken);
            }
        }

        private async Task ProcessWeatherAsync(ITurnContext<IMessageActivity> turnContext, LuisResult luisResult, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProcessWeatherAsync");

            // Retrieve LUIS results for Weather.
            //var result = luisResult.ConnectedServiceResult;
            //var topIntent = result.TopScoringIntent.Intent;
            var topIntent = luisResult.TopScoringIntent.Intent;
            await turnContext.SendActivityAsync(MessageFactory.Text($"ProcessWeather top intent {topIntent}."), cancellationToken);
            await turnContext.SendActivityAsync(MessageFactory.Text($"ProcessWeather Intents detected::\n\n{string.Join("\n\n", luisResult.Intents.Select(i => i.Intent))}"), cancellationToken);
            if (luisResult.Entities.Count > 0)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"ProcessWeather entities were found in the message:\n\n{string.Join("\n\n", luisResult.Entities.Select(i => i.Entity+"-->"+ i.Type))}"), cancellationToken);
            }
        }

        private async Task ProcessSampleQnAAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProcessSampleQnAAsync");

            var results = await _botServices.SampleQnA.GetAnswersAsync(turnContext);
            if (results.Any())
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(results.First().Answer), cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Sorry, could not find an answer in the Q and A system."), cancellationToken);
            }
        }
    }
}
