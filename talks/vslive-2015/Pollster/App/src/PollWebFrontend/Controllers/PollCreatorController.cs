using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;

using Pollster.CommonCode;

using Pollster.PollWebFrontend.Clients;
using Pollster.PollWebFrontend.Models;

namespace Pollster.PollWebFrontend.Controllers
{
    public class PollCreatorController : Controller
    {
        IPollWriter _pollWriter;

        public PollCreatorController(IPollWriter writer)
        {
            this._pollWriter = writer;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new PollCreatorViewModel
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(1)
            };

            model.StartTime = DateTime.Now.AddMinutes(10);
            model.EndTime = DateTime.Now.AddMinutes(10);

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(PollCreatorViewModel model)
        {
            PollConfirmationViewModel confirmModel = new PollConfirmationViewModel();
            try
            {
                var pollDefinition = new PollDefinition
                {
                    Title = model.Title,
                    Question = model.Question,
                    AuthorEmail = model.AuthorEmail,
                    Options = new Dictionary<string, PollDefinition.Option>()
                };
                pollDefinition.StartTime = model.StartDate.Date.Add(model.StartTime.TimeOfDay);
                pollDefinition.EndTime = model.EndDate.Date.Add(model.EndTime.TimeOfDay);

                string line;
                using (var reader = new StringReader(model.Options))
                {
                    int index = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        pollDefinition.Options.Add(index.ToString(), new PollDefinition.Option { Text = line });
                        index++;
                    }
                }

                await this._pollWriter.Save(pollDefinition);

                confirmModel.Title = pollDefinition.Title;
                confirmModel.StartTime = pollDefinition.StartTime;
                confirmModel.EndTime = pollDefinition.EndTime;
                confirmModel.Success = true;
            }
            catch(Exception e)
            {
                confirmModel.Success = false;
                confirmModel.ErrorMessage = string.Format("Unknown error saving poll: {0}", e.Message);
            }

            return View("Confirmation", confirmModel);
        }
    }
}
