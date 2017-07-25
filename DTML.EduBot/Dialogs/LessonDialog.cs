﻿namespace DTML.EduBot.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AdaptiveCards;
    using DTML.EduBot.LessonPlan;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;
    using Models;

    [Serializable]
    public class LessonDialog : IDialog<string>
    {
        private readonly int MAX_ATTEMPT = 2;
        private Lesson lesson;

        public LessonDialog(Lesson lesson)
        {
            this.lesson = lesson;
        }

        public async Task StartAsync(IDialogContext context)
        {
            var wasSuccess = await PostAdaptiveCard(context);
            if (!wasSuccess)
            {
                context.Done(Constants.Shared.NoMoreLessonsMessage);
                return;
            }

            context.Wait(this.CheckAnswerOptionsAsync);
        }

        private async Task<bool> PostAdaptiveCard(IDialogContext context)
        {
            var nextTopic = lesson.Topics.ElementAtOrDefault(lesson.currentTopic);
            if (nextTopic == null)
            {
                return false;
            }

            List<ActionBase> answerOptions = PopulateActionBasesFromAnswerOptions(nextTopic.AnswerOptions);

            AdaptiveCard adaptiveCard = new AdaptiveCard()
            {
                Body = new List<CardElement>()
                {
                    new TextBlock()
                    {
                        Text = nextTopic.Question,
                        Wrap = true
                    },
                    new Image()
                    {
                        Size = ImageSize.Large,
                        Url  = nextTopic.ImageUrl
                    },
                },
                Actions = answerOptions
            };

            Attachment attachment = new Attachment()
            {
                ContentType = AdaptiveCard.ContentType,
                Content = adaptiveCard
            };

            var reply = context.MakeMessage();
            reply.Attachments.Add(attachment);

            await context.PostAsync(reply, CancellationToken.None);
            return true;
        }

        private List<ActionBase> PopulateActionBasesFromAnswerOptions(ICollection<string> answerOptions)
        {
            List<ActionBase> actionBases = new List<ActionBase>();

            foreach (string answer in answerOptions)
            {
                actionBases.Add(
                    new SubmitAction()
                    {
                        Title = answer,
                        DataJson = "{\"answer\": \"" + answer + "\"}"
                    }
                );
            }

            return actionBases;
        }

        private async Task CheckAnswerOptionsAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            dynamic response = ExtractMessageValue(result);
            StudentResponse studentResponse = StudentResponse.FromDynamic(response);
            
            var topic = lesson.Topics.ElementAtOrDefault(lesson.currentTopic);
            if (topic == null)
            {
                return;
            }

            if (studentResponse.Answer != null && studentResponse.Answer.Equals(topic.CorrectAnswer, StringComparison.InvariantCultureIgnoreCase))
            {
                await context.PostAsync(topic.CorrectAnswerBotResponse);
                context.Wait(this.CheckTypedAnswerAsync);
            }
            else
            {
                await context.PostAsync(topic.WrongAnswerBotResponse);
                await this.StartAsync(context);
            }
        }

        private async Task CheckTypedAnswerAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            StudentResponse studentResponse = new StudentResponse(message.Text);

            var topic = lesson.Topics.ElementAtOrDefault(lesson.currentTopic);
            if (topic == null)
            {
                return;
            }

            if (studentResponse.Answer != null && studentResponse.Answer.Equals(topic.CorrectAnswer, StringComparison.InvariantCultureIgnoreCase))
            {
                await this.PronounceLearnedPhrase(context, result);
            }
            else
            {
                await context.PostAsync(topic.WrongAnswerBotResponse);
                await this.StartAsync(context);
            }
        }

        private async Task PronounceLearnedPhrase(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var topic = lesson.Topics.ElementAtOrDefault(lesson.currentTopic);
            if (topic == null)
            {
                return;
            }

            await this.PostAudioInstruction(context, topic);

            await this.WrapUpCurrentTopic(context, topic);
        }

        private async Task PostAudioInstruction(IDialogContext context, Topic topic)
        {
            await context.PostAsync(topic.PronounciationPhrase);

            // client handling at: https://github.com/eklavyamirani/BotFramework-WebChat/commit/a0cc2cf87563414c558691583788bbd8e8c8f6a2
            await context.SayAsync(topic.CorrectAnswer, topic.CorrectAnswer);
        }

        private Task WrapUpCurrentTopic(IDialogContext context, Topic topic)
        {
            ICollection<string> wrapUpPhrases = new List<string>();
            wrapUpPhrases.Add(topic.NextTopicPhrase);
            wrapUpPhrases.Add(topic.StayOnCurrentTopicPhrase);

            PromptDialog.Choice(
                context,
                this.AfterWrapUpCurrentTopic,
                wrapUpPhrases,
                "This marks the end of current topic!",
                "I am sorry but I didn't understand that. I need you to select one of the options below",
                attempts: MAX_ATTEMPT);

            return Task.CompletedTask;
        }

        private async Task AfterWrapUpCurrentTopic(IDialogContext context, IAwaitable<string> result)
        {
            var topic = lesson.Topics.ElementAtOrDefault(lesson.currentTopic);
            if (topic == null)
            {
                return;
            }

            try
            {
                var selection = await result as string;
                
                if (topic.NextTopicPhrase.Equals(selection))
                {
                    if (lesson.currentTopic >= lesson.Topics.Count - 1)
                        // reaching the end of the topic for current lesson
                    {
                        context.Done("This is the end of the current lesson. Thank you!");
                    }
                    else
                    {
                        lesson.currentTopic++;
                        await this.StartAsync(context);
                    }
                }
                else if (topic.StayOnCurrentTopicPhrase.Equals(selection))
                {
                    await this.PronounceLearnedPhrase(context, null);
                }
            }
            catch (TooManyAttemptsException)
            {
                await this.StartAsync(context);
            }
        }

        private async Task<dynamic> ExtractMessageValue(IAwaitable<IMessageActivity> result)
        {
            var message = await result;

            if (message.Value == null)
            {
                return null;
            }

            dynamic response = message.Value;

            return response;
        }
    }
}