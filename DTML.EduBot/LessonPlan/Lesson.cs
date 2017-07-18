﻿using Newtonsoft.Json;

namespace DTML.EduBot.LessonPlan
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class Lesson
    {
        public string LessonName { get; set; }

        private ICollection<Topic> topics = new List<Topic>();

        [JsonProperty("lesson_title", Required = Required.Always)]
        public string LessonTitle { get; set; }

        [JsonProperty("topics", Required = Required.Always)]
        public ICollection<Topic> Topics
        {
            get { return topics; }
        }
    }
}