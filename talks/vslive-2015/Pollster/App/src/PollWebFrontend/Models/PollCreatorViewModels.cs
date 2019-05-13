using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Pollster.CommonCode;

namespace Pollster.PollWebFrontend.Models
{
    public class PollCreatorViewModel
    {
        public string Title { get; set; }
        public string Question { get; set; }
        public string AuthorEmail { get; set; }
        public string Options { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime EndTime { get; set; }
    }


    public class PollConfirmationViewModel
    {
        public string Title { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}
