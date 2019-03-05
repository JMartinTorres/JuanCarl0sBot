// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Schema;

namespace NLP_With_Dispatch_Bot
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each interaction from the user, an instance of this class is called.
    /// This is a Transient lifetime service. Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single Turn, should be carefully managed.
    /// </summary>
    public class JuanCarlosBot : IBot
    {
        private const string WelcomeText = "Soy JuanCarl0sBot, ¿en qué puedo ayudarte?";

        /// <summary>
        /// Key in the Bot config (.bot file) for the Home Automation Luis instance.
        /// </summary>
        /// 

        // Dispatch keys
        private const string CampusDispatchKey = "I_Campus";
        private const string myAppsDispatchKey = "MyApps";
        private const string noneDispatchKey = "None";
        private const string qnaDispatchKey = "QnA";

        // LUIS keys
        private const string ICampusLuisKey = "Información_general";
        private const string CampusDistintosLuisKey = "Campus_distintos";
        private const string MyAppsLuisKey = "Acceso MyApps";

        /// <summary>
        /// Key in the Bot config (.bot file) for the Dispatch.
        /// </summary>
        private const string DispatchKey = "Dispatch";

        /// <summary>
        /// Key in the Bot config (.bot file) for the QnaMaker instance.
        /// In the .bot file, multiple instances of QnaMaker can be configured.
        /// </summary>
        private const string QnAMakerKey = "urjcbot-qna";

        /// <summary>
        /// Services configured from the ".bot" file.
        /// </summary>
        private readonly BotServices _services;

        // The bot state accessor object. Use this to access specific state properties.
        private readonly WelcomeUserStateAccessors _welcomeUserStateAccessors;
        private readonly CustomPromptBotAccessors _promptAccessors;

        /// <summary>
        /// Initializes a new instance of the <see cref="JuanCarlosBot"/> class.
        /// </summary>
        /// <param name="services">Services configured from the ".bot" file.</param>
        /// <param name="statePropertyAccessor"> Bot state accessor object.</param>
        public JuanCarlosBot(BotServices services, WelcomeUserStateAccessors statePropertyAccessor)
        {
            _services = services ?? throw new System.ArgumentNullException(nameof(services));

            if (!_services.QnAServices.ContainsKey(QnAMakerKey))
            {
                throw new System.ArgumentException($"Invalid configuration. Please check your '.bot' file for a QnA service named '{DispatchKey}'.");
            }

            if (!_services.LuisServices.ContainsKey(CampusDispatchKey))
            {
                throw new System.ArgumentException($"Invalid configuration. Please check your '.bot' file for a Luis service named '{CampusDispatchKey}'.");
            }

            _welcomeUserStateAccessors = statePropertyAccessor ?? throw new System.ArgumentNullException("state accessor can't be null");
            //_promptAccessors = _promptAccessors ?? throw new System.ArgumentNullException(nameof(_promptAccessors));
        }

        /// <summary>
        /// Every conversation turn for our NLP Dispatch Bot will call this method.
        /// There are no dialogs used, since it's "single turn" processing, meaning a single
        /// request and response, with no stateful conversation.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {

            //// use state accessor to extract the didBotWelcomeUser flag
            //var didBotWelcomeUser = await _welcomeUserStateAccessors.WelcomeUserState.GetAsync(turnContext, () => new WelcomeUserState());
            //if (didBotWelcomeUser.DidBotWelcomeUser == false && !welcomed)
            //{
            //    didBotWelcomeUser.DidBotWelcomeUser = true;
            //    welcomed = true;
            //    // Update user state flag to reflect bot handled first user interaction.
            //    await _welcomeUserStateAccessors.WelcomeUserState.SetAsync(turnContext, didBotWelcomeUser);
            //    await _welcomeUserStateAccessors.UserState.SaveChangesAsync(turnContext);

            //    // the channel should sends the user name in the 'From' object
            //    var userName = turnContext.Activity.From.Name;

            //    await turnContext.SendActivityAsync($"You are seeing this message because this was your first message ever to this bot.", cancellationToken: cancellationToken);
            //    await turnContext.SendActivityAsync($"It is a good practice to welcome the user and provide personal greeting. For example, welcome {userName}.", cancellationToken: cancellationToken);
            //}

            if (turnContext.Activity.Type == ActivityTypes.Message && !turnContext.Responded)
            {
                // Get the intent recognition result
                var recognizerResult = await _services.LuisServices[DispatchKey].RecognizeAsync(turnContext, cancellationToken);
                var topIntent = recognizerResult?.GetTopScoringIntent();

                if (topIntent == null)
                {
                    await turnContext.SendActivityAsync("Unable to get the top intent.");
                }
                else
                {
                    await DispatchToTopIntentAsync(turnContext, topIntent, cancellationToken);
                }
            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                // Send a welcome message to the user and tell them what actions they may perform to use this bot
                if (turnContext.Activity.MembersAdded != null)
                {
                    await SendWelcomeMessageAsync(turnContext, cancellationToken);
                }
            }
            else
            {
                await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected", cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// On a conversation update activity sent to the bot, the bot will
        /// send a message to the any new user(s) that were added.
        /// </summary>
        /// <param name="turnContext">Provides the <see cref="ITurnContext"/> for the turn of the bot.</param>
        /// <param name="cancellationToken" >(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>>A <see cref="Task"/> representing the operation result of the Turn operation.</returns>
        private static async Task SendWelcomeMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(
                        $"Bienvenido, {member.Name}. {WelcomeText}",
                        cancellationToken: cancellationToken);
                }
            }
        }

        /// <summary>
        /// Depending on the intent from Dispatch, routes to the right LUIS model or QnA service.
        /// </summary>
        private async Task DispatchToTopIntentAsync(ITurnContext context, (string intent, double score)? topIntent, CancellationToken cancellationToken = default(CancellationToken))
        {
            // No se reconoce ningún intent.
            if (topIntent.Value.intent == noneDispatchKey)
            {
                await context.SendActivityAsync($"Dispatch intent: {topIntent.Value.intent} ({topIntent.Value.score}).");
            }

            // Intent reconocido se envía a función de procesado de apps LUIS.
            else
            {
                var intent = topIntent.Value.intent;
                if (intent.Contains(":"))
                {
                    await DispatchToLuisModelAsync(context, intent.Substring(0, intent.IndexOf(":")));
                }
                else
                {
                    await DispatchToLuisModelAsync(context, intent);
                }
            }
        }

        /// <summary>
        /// Dispatches the turn to the request QnAMaker app.
        /// </summary>
        private async Task DispatchToQnAMakerAsync(ITurnContext context, string appName, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!string.IsNullOrEmpty(context.Activity.Text))
            {
                var results = await _services.QnAServices[appName].GetAnswersAsync(context);
                if (results.Any())
                {
                    await context.SendActivityAsync(results.First().Answer, cancellationToken: cancellationToken);
                }
                else
                {
                    await context.SendActivityAsync($"Perdón, no te he entendido.");
                }
            }
        }

        private async Task<string> DispatchToQnAMakerTextAsync(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!string.IsNullOrEmpty(context.Activity.Text))
            {
                var results = await _services.QnAServices[QnAMakerKey].GetAnswersAsync(context);
                if (results.Any())
                {
                    return results.First().Answer;
                }
                else
                {
                    await context.SendActivityAsync($"Perdón, no te he entendido.");
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Dispatches the turn to the requested LUIS model.
        /// </summary>
        private async Task DispatchToLuisModelAsync(ITurnContext context, string appName, CancellationToken cancellationToken = default(CancellationToken))
        {
            // await context.SendActivityAsync($"Sending your request to the {appName} system ...");
            var result = await _services.LuisServices[appName].RecognizeAsync(context, cancellationToken);

            // Nombre y puntuación de cada intent reconocido
            foreach (var intent in result.Intents)
            {
                // await context.SendActivityAsync($"Intents detected by the {appName} app:\n\n{string.Join("\n\n", "Intent: " + intent.Key, "Score: " + intent.Value.Score)}");
            }

            if (result.Intents.Count > 0)
            {
                var intent = result.Intents.First().Key.ToString();

                switch (appName)
                {
                    case CampusDispatchKey:
                        await ProcessICampusModelAsync(context, result, cancellationToken);
                        break;
                    default:
                        context.Activity.Text = intent;
                        await context.SendActivityAsync(await DispatchToQnAMakerTextAsync(context, cancellationToken));
                        break;
                }
            }
            else
            {
                await context.SendActivityAsync($"Perdón, no te he entendido.");
            }
        }

        private async Task ProcessICampusModelAsync(ITurnContext context, RecognizerResult result, CancellationToken cancellationToken = default(CancellationToken))
        {
            var intent = result.Intents.First().Key.ToString();

            // Resolver intent de información general de campus
            switch (intent)
            {
                case ICampusLuisKey:
                    await ProcessICampusIntentAsync(context, result, cancellationToken);
                    break;
                default:
                    context.Activity.Text = intent;
                    await context.SendActivityAsync(await DispatchToQnAMakerTextAsync(context, cancellationToken));
                    break;
            }
        }

        private async Task ProcessICampusIntentAsync(ITurnContext context, RecognizerResult result, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (result.Entities.Count > 1)
            {
                context.Activity.Text = result.Intents.First().Key.ToString();
                string campus = result.Entities.Last.Last.ToString();
                if (campus != string.Empty)
                {
                    campus = ClearEntity(campus);

                    string msg = await DispatchToQnAMakerTextAsync(context, cancellationToken);
                    msg = msg.Replace("$campus", campus.First().ToString().ToUpper() + campus.Substring(1));
                    msg = msg.Replace("@campus", campus.ToLower());

                    await context.SendActivityAsync(msg);
                }
            }
            else
            {
                await context.SendActivityAsync($"Perdón, no te he entendido.");
            }
        }

        private string ClearEntity(string entity)
        {
            var charsToRemove = new string[] { "\r", "\n", "\"", "[", "]", " " };
            foreach (var c in charsToRemove)
            {
                entity = entity.Replace(c, string.Empty);
            }

            return entity;
        }
    }
}
