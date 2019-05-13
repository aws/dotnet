using System;
using System.Collections.Generic;
using System.Text;

namespace Pollster.Workflow
{
    public class PollState
    {
        public string PollId { get; set; }

        public long SecondsTillActive { get; set; }

        public long SecondsTillDeactivate { get; set; }
    }
}
