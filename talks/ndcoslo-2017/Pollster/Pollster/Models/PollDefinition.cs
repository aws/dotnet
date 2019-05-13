using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Pollster.Models
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

        [Required]
        public string Title { get; set; }
        [Required]
        public string Question { get; set; }

        [Required]
        public Dictionary<string, Option> Options { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
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

            [Required]
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
