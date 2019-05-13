using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pollster.CommonCode
{
    public static class Constants
    {
        public const string SWF_DOMAIN = "Pollster";
        public const string SWF_WORKFLOW_TYPE_NAME = "PollScheduler";
        public const string SWF_DECIDER_TASKLIST = "scheduler";

        public const string SWF_ACTIVTY_START_TIMER_EXPIRED = "StartTimerExpired";
        public const string SWF_ACTIVTY_START_TIMER_EXPIRED_VERSION = "1.0";
        public const string SWF_ACTIVTY_START_TIMER_EXPIRED_TASKLIST = "StartTimerExpiredTasks";

        public const string SWF_ACTIVTY_END_TIMER_EXPIRED = "EndTimerExpired";
        public const string SWF_ACTIVTY_END_TIMER_EXPIRED_VERSION = "1.0";
        public const string SWF_ACTIVTY_END_TIMER_EXPIRED_TASKLIST = "ENDTimerExpiredTasks";

        public const string SWF_TIMEOUT = "300";
    }
}
