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
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.Number;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;

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

        public class Reservation
        {
            public int Size { get; set; }

            public string Date { get; set; }
        }

        private const string WelcomeText = "Soy JuanCarl0sBot, ¿en qué puedo ayudarte?";
        private const string ReservationDialog = "reservationDialog";
        private const string PartySizePrompt = "partyPrompt";
        private const string LocationPrompt = "locationPrompt";
        private const string ReservationDatePrompt = "reservationDatePrompt";

        /// <summary>
        /// Key in the Bot config (.bot file) for the Home Automation Luis instance.
        /// </summary>
        /// 

        // Dispatch keys
        private const string CampusDispatchKey = "I_Campus";
        private const string NoneDispatchKey = "None";
        private const string QnaDispatchKey = "QnA";

        // LUIS keys
        private const string ICampusLuisKey = "Información_general";

        /// <summary>
        /// Key in the Bot config (.bot file) for the Dispatch.
        /// </summary>
        private const string DispatchKey = "Dispatch";

        /// <summary>
        /// Key in the Bot config (.bot file) for the QnaMaker instance.
        /// In the .bot file, multiple instances of QnaMaker can be configured.
        /// </summary>
        private const string QnAMakerKey = "urjcbot-qna";

        private static DialogSet _dialogSet;
        private static ConversationFlowDialog testcfd = new ConversationFlowDialog();
        private static bool done = true;

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
        /// <param name="promptBotAccessors"> Prompt state accessor object.</param>
        public JuanCarlosBot(BotServices services, WelcomeUserStateAccessors statePropertyAccessor, CustomPromptBotAccessors promptBotAccessors)
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
            _promptAccessors = promptBotAccessors ?? throw new System.ArgumentNullException(nameof(_promptAccessors));

            // Create the dialog set and add the prompts, including custom validation.
            _dialogSet = new DialogSet(_welcomeUserStateAccessors.DialogStateAccessor);
            _dialogSet.Add(new NumberPrompt<int>(PartySizePrompt, PartySizeValidatorAsync));
            _dialogSet.Add(new ChoicePrompt(LocationPrompt));
            _dialogSet.Add(new DateTimePrompt(ReservationDatePrompt, DateValidatorAsync));

            // Define the steps of the waterfall dialog and add it to the set.
            WaterfallStep[] steps = new WaterfallStep[]
            {
                PromptForPartySizeAsync,
                PromptForLocationAsync,
                PromptForReservationDateAsync,
                AcknowledgeReservationAsync,
            };
            _dialogSet.Add(new WaterfallDialog(ReservationDialog, steps));

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

            if (turnContext.Activity.Type == ActivityTypes.Message && !turnContext.Responded)
            {
                if (!done)
                {
                    // Get the current reservation info from state.
                    Reservation reservation = await _welcomeUserStateAccessors.ReservationAccessor.GetAsync(
                        turnContext, () => null, cancellationToken);

                    // Generate a dialog context for our dialog set.
                    DialogContext dc = await _dialogSet.CreateContextAsync(turnContext, cancellationToken);

                    if (dc.ActiveDialog is null)
                    {
                        // If there is no active dialog, check whether we have a reservation yet.
                        if (reservation is null)
                        {
                            // If not, start the dialog.
                            await dc.BeginDialogAsync(ReservationDialog, null, cancellationToken);
                        }
                        else
                        {
                            // Otherwise, send a status message.
                            await turnContext.SendActivityAsync(
                                $"We'll see you on {reservation.Date}.",
                                cancellationToken: cancellationToken);
                        }
                    }
                    else
                    {
                        // Continue the dialog.
                        DialogTurnResult dialogTurnResult = await dc.ContinueDialogAsync(cancellationToken);

                        // If the dialog completed this turn, record the reservation info.
                        if (dialogTurnResult.Status is DialogTurnStatus.Complete)
                        {
                            reservation = (Reservation)dialogTurnResult.Result;
                            await _welcomeUserStateAccessors.ReservationAccessor.SetAsync(
                                turnContext,
                                reservation,
                                cancellationToken);

                            // Send a confirmation message to the user.
                            await turnContext.SendActivityAsync(
                                $"Your party of {reservation.Size} is confirmed for {reservation.Date}.",
                                cancellationToken: cancellationToken);
                        }
                    }

                    // Save the updated dialog state into the conversation state.
                    await _welcomeUserStateAccessors.ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
                }
                else
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
            if (topIntent.Value.intent == NoneDispatchKey)
            {
                await context.SendActivityAsync($"Perdón, no te he entendido.");
            }

            // Pregunta guardada en QnA
            else if (topIntent.Value.intent == QnaDispatchKey)
            {
                await DispatchToQnAMakerAsync(context, cancellationToken);
            }

            // Intent reconocido se envía a función de procesado de apps LUIS.
            else
            {
                var intent = topIntent.Value.intent;
                if (intent.Contains(";"))
                {
                    await DispatchToLuisModelAsync(context, intent.Substring(0, intent.IndexOf(";")));
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
        private async Task DispatchToQnAMakerAsync(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!string.IsNullOrEmpty(context.Activity.Text))
            {
                var results = await _services.QnAServices[QnAMakerKey].GetAnswersAsync(context);
                if (results.Any())
                {
                    if (results.First().Answer.StartsWith("#@$RESET#$@"))
                    {
                        //flowQuestions.Add(results.First().Answer.Substring(0, 20));
                        done = false;
                    }
                    else
                    {
                        await context.SendActivityAsync(results.First().Answer, cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    await context.SendActivityAsync($"Perdón, no te he entendido.");
                }
            }
        }

        private async Task<string> DispatchToQnAMakerTextAsync(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken), bool firstTry = false)
        {
            if (!string.IsNullOrEmpty(context.Activity.Text))
            {
                var results = await _services.QnAServices[QnAMakerKey].GetAnswersAsync(context);
                if (results.Any())
                {
                    return results.First().Answer;
                }
                else if (!firstTry)
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

        private async Task<DialogTurnResult> PromptForPartySizeAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Prompt for the party size. The result of the prompt is returned to the next step of the waterfall.




            return await stepContext.PromptAsync(
                 PartySizePrompt,
                 new PromptOptions
                 {
                     Prompt = MessageFactory.Text("How many people is the reservation for?"),
                     RetryPrompt = MessageFactory.Text("How large is your party?"),
                 },
                 cancellationToken);

        }

        private async Task<DialogTurnResult> PromptForLocationAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Record the party size information in the current dialog state.
            int size = (int)stepContext.Result;
            stepContext.Values["size"] = size;

            return await stepContext.PromptAsync(
                "locationPrompt",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Please choose a location."),
                    RetryPrompt = MessageFactory.Text("Sorry, please choose a location from the list."),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Redmond", "Bellevue", "Seattle" }),
                },
                cancellationToken);
        }

        /// <summary>Second step of the main dialog: record the party size and prompt for the
        /// reservation date.</summary>
        /// <param name="stepContext">The context for the waterfall step.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task is successful, the result contains information from this step.</remarks>
        private async Task<DialogTurnResult> PromptForReservationDateAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Record the party size information in the current dialog state.
            var location = stepContext.Result;
            stepContext.Values["location"] = location;

            // Prompt for the party size. The result of the prompt is returned to the next step of the waterfall.
            return await stepContext.PromptAsync(
                ReservationDatePrompt,
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Great. When will the reservation be for?"),
                    RetryPrompt = MessageFactory.Text("What time should we make your reservation for?"),
                },
                cancellationToken);
        }

        /// <summary>Third step of the main dialog: return the collected party size and reservation date.</summary>
        /// <param name="stepContext">The context for the waterfall step.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task is successful, the result contains information from this step.</remarks>
        private async Task<DialogTurnResult> AcknowledgeReservationAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Retrieve the reservation date.
            DateTimeResolution resolution = (stepContext.Result as IList<DateTimeResolution>).First();
            string time = resolution.Value ?? resolution.Start;

            // Send an acknowledgement to the user.
            await stepContext.Context.SendActivityAsync(
                "Thank you. We will confirm your reservation shortly.",
                cancellationToken: cancellationToken);

            // Return the collected information to the parent context.
            Reservation reservation = new Reservation
            {
                Date = time,
                Size = (int)stepContext.Values["size"],
            };
            done = true;
            return await stepContext.EndDialogAsync(reservation, cancellationToken);
        }

        /// <summary>Validates whether the party size is appropriate to make a reservation.</summary>
        /// <param name="promptContext">The validation context.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>Reservations can be made for groups of 6 to 20 people.
        /// If the task is successful, the result indicates whether the input was valid.</remarks>
        private async Task<bool> PartySizeValidatorAsync(
            PromptValidatorContext<int> promptContext,
            CancellationToken cancellationToken)
        {
            // Check whether the input could be recognized as an integer.
            if (!promptContext.Recognized.Succeeded)
            {
                await promptContext.Context.SendActivityAsync(
                    "I'm sorry, I do not understand. Please enter the number of people in your party.",
                    cancellationToken: cancellationToken);
                return false;
            }

            // Check whether the party size is appropriate.
            int size = promptContext.Recognized.Value;
            if (size < 6 || size > 20)
            {
                await promptContext.Context.SendActivityAsync(
                    "Sorry, we can only take reservations for parties of 6 to 20.",
                    cancellationToken: cancellationToken);
                return false;
            }

            return true;
        }

        /// <summary>Validates whether the reservation date is appropriate.</summary>
        /// <param name="promptContext">The validation context.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>Reservations must be made at least an hour in advance.
        /// If the task is successful, the result indicates whether the input was valid.</remarks>
        private async Task<bool> DateValidatorAsync(
            PromptValidatorContext<IList<DateTimeResolution>> promptContext,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Check whether the input could be recognized as an integer.
            if (!promptContext.Recognized.Succeeded)
            {
                await promptContext.Context.SendActivityAsync(
                    "I'm sorry, I do not understand. Please enter the date or time for your reservation.",
                    cancellationToken: cancellationToken);
                return false;
            }

            // Check whether any of the recognized date-times are appropriate,
            // and if so, return the first appropriate date-time.
            DateTime earliest = DateTime.Now.AddHours(1.0);
            DateTimeResolution value = promptContext.Recognized.Value.FirstOrDefault(v =>
                DateTime.TryParse(v.Value ?? v.Start, out DateTime time) && DateTime.Compare(earliest, time) <= 0);
            if (value != null)
            {
                promptContext.Recognized.Value.Clear();
                promptContext.Recognized.Value.Add(value);
                return true;
            }

            await promptContext.Context.SendActivityAsync(
                    "I'm sorry, we can't take reservations earlier than an hour from now.",
                    cancellationToken: cancellationToken);
            return false;
        }

    }
}

//////// Welcome (evitar que el usuario tenga que iniciar la conversación, no logrado)

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


///////// ConversationFlow (validaciones; ¿prompt de opciones no es posible?)
//        private static async Task FillOutUserProfileAsync(ConversationFlow flow, UserProfile profile, ITurnContext turnContext,
//                    CancellationToken cancellationToken = default(CancellationToken))
//        {
//            string input = turnContext.Activity.Text?.Trim();
//            string message;
//            switch (flow.LastQuestionAsked)
//            {
//                case ConversationFlow.Question.None:
//                    //await turnContext.SendActivityAsync(flowQuestions.ElementAt(flowQuestions.Count-1));
//                    flow.LastQuestionAsked = ConversationFlow.Question.Name;
//                    break;
//                case ConversationFlow.Question.Name:
//                    if (ValidateName(input, out string name, out message))
//                    {
//                        profile.Name = name;
//                        await turnContext.SendActivityAsync($"Hi {profile.Name}.");
//                        await turnContext.SendActivityAsync("How old are you?");
//                        flow.LastQuestionAsked = ConversationFlow.Question.Age;
//                        break;
//                    }
//                    else
//                    {
//                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.");
//                        break;
//                    }

//                case ConversationFlow.Question.Age:
//                    if (ValidateAge(input, out int age, out message))
//                    {
//                        profile.Age = age;
//                        await turnContext.SendActivityAsync($"I have your age as {profile.Age}.");
//                        await turnContext.SendActivityAsync("When is your flight?");
//                        flow.LastQuestionAsked = ConversationFlow.Question.Date;
//                        break;
//                    }
//                    else
//                    {
//                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.");
//                        break;
//                    }

//                case ConversationFlow.Question.Date:
//                    if (ValidateDate(input, out string date, out message))
//                    {
//                        profile.Date = date;
//                        await turnContext.SendActivityAsync($"Your cab ride to the airport is scheduled for {profile.Date}.");
//                        await turnContext.SendActivityAsync($"Thanks for completing the booking {profile.Name}.");
//                        await turnContext.SendActivityAsync($"Type anything to run the bot again.");
//                        flow.LastQuestionAsked = ConversationFlow.Question.None;
//                        done = true;
//                        profile = new UserProfile();
//                        break;
//                    }
//                    else
//                    {
//                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.");
//                        break;
//                    }
//            }
//        }

//        /// <summary>
//        /// Validates name input.
//        /// </summary>
//        /// <param name="input">The user's input.</param>
//        /// <param name="name">When the method returns, contains the normalized name, if validation succeeded.</param>
//        /// <param name="message">When the method returns, contains a message with which to reprompt, if validation failed.</param>
//        /// <returns>indicates whether validation succeeded.</returns>
//        private static bool ValidateName(string input, out string name, out string message)
//        {
//            name = null;
//            message = null;

//            if (string.IsNullOrWhiteSpace(input))
//            {
//                message = "Please enter a name that contains at least one character.";
//            }
//            else
//            {
//                name = input.Trim();
//            }

//            return message is null;
//        }

//        /// <summary>
//        /// Validates age input.
//        /// </summary>
//        /// <param name="input">The user's input.</param>
//        /// <param name="age">When the method returns, contains the normalized age, if validation succeeded.</param>
//        /// <param name="message">When the method returns, contains a message with which to reprompt, if validation failed.</param>
//        /// <returns>indicates whether validation succeeded.</returns>
//        private static bool ValidateAge(string input, out int age, out string message)
//        {
//            age = 0;
//            message = null;

//            // Try to recognize the input as a number. This works for responses such as "twelve" as well as "12".
//            try
//            {
//                // Attempt to convert the Recognizer result to an integer. This works for "a dozen", "twelve", "12", and so on.
//                // The recognizer returns a list of potential recognition results, if any.
//                List<ModelResult> results = NumberRecognizer.RecognizeNumber(input, Culture.English);
//                foreach (ModelResult result in results)
//                {
//                    // The result resolution is a dictionary, where the "value" entry contains the processed string.
//                    if (result.Resolution.TryGetValue("value", out object value))
//                    {
//                        age = Convert.ToInt32(value);
//                        if (age >= 18 && age <= 120)
//                        {
//                            return true;
//                        }
//                    }
//                }

//                message = "Please enter an age between 18 and 120.";
//            }
//            catch
//            {
//                message = "I'm sorry, I could not interpret that as an age. Please enter an age between 18 and 120.";
//            }

//            return message is null;
//        }

//        /// <summary>
//        /// Validates flight time input.
//        /// </summary>
//        /// <param name="input">The user's input.</param>
//        /// <param name="date">When the method returns, contains the normalized date, if validation succeeded.</param>
//        /// <param name="message">When the method returns, contains a message with which to reprompt, if validation failed.</param>
//        /// <returns>indicates whether validation succeeded.</returns>
//        private static bool ValidateDate(string input, out string date, out string message)
//        {
//            date = null;
//            message = null;

//            // Try to recognize the input as a date-time. This works for responses such as "11/14/2018", "9pm", "tomorrow", "Sunday at 5pm", and so on.
//            // The recognizer returns a list of potential recognition results, if any.
//            try
//            {
//                List<ModelResult> results = DateTimeRecognizer.RecognizeDateTime(input, Culture.English);

//                // Check whether any of the recognized date-times are appropriate,
//                // and if so, return the first appropriate date-time. We're checking for a value at least an hour in the future.
//                DateTime earliest = DateTime.Now.AddHours(1.0);
//                foreach (ModelResult result in results)
//                {
//                    // The result resolution is a dictionary, where the "values" entry contains the processed input.
//                    List<Dictionary<string, string>> resolutions = result.Resolution["values"] as List<Dictionary<string, string>>;
//                    foreach (Dictionary<string, string> resolution in resolutions)
//                    {
//                        // The processed input contains a "value" entry if it is a date-time value, or "start" and
//                        // "end" entries if it is a date-time range.
//                        if (resolution.TryGetValue("value", out string dateString)
//                            || resolution.TryGetValue("start", out dateString))
//                        {
//                            if (DateTime.TryParse(dateString, out DateTime candidate)
//                                && earliest < candidate)
//                            {
//                                date = candidate.ToShortDateString();
//                                return true;
//                            }
//                        }
//                    }
//                }

//                message = "I'm sorry, please enter a date at least an hour out.";
//            }
//            catch
//            {
//                message = "I'm sorry, I could not interpret that as an appropriate date. Please enter a date at least an hour out.";
//            }

//            return false;
//        }
//    }
//}