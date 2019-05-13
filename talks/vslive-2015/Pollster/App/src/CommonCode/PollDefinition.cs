using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pollster.CommonCode
{
    public class PollDefinition
    {
        public const string POLL_STATE_UNSCHEDULE = "Unscheduled";
        public const string POLL_STATE_SCHEDULE = "Scheduled";
        public const string POLL_STATE_ACTIVE = "Active";
        public const string POLL_STATE_EXPIRED = "Expired";

        public PollDefinition()
        {
            this.State = POLL_STATE_UNSCHEDULE;
        }

        public string Id { get; set; }
        public string AuthorEmail { get; set; }
        public string Title { get; set; }
        public string Question { get; set; }
        public string TopicArn { get; set; }

        public Dictionary<string, Option> Options { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string State { get; set; }

        public class Option
        {
            public Option()
            {

            }

            public Option(string text)
            {
                this.Text = text;
            }

            public string Text { get; set; }
            public int Votes { get; set; }
        }
    }

    public class ActivePoll
    {
        public string Id { get; set; }

        public DateTime ActivatedTime { get; set; }
    }
}
