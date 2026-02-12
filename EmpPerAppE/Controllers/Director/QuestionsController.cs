using EmpPerAppE.Models;
using EmpPerAppE.Models.DTO;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;

namespace EmpPerAppE.Controllers.Director
{
    [RoutePrefix("api/Questions")]
    public class QuestionsController : ApiController
    {
        private fypempEntities db = new fypempEntities();

        public QuestionsController()
        {
            // Circular reference errors se bachne ke liye
            db.Configuration.ProxyCreationEnabled = false;
            db.Configuration.LazyLoadingEnabled = false;
        }

        // 1. CREATE: Naya Questionnaire aur uske Questions save karne ke liye
        [HttpPost]
        [Route("Create")]
        public IHttpActionResult CreateQuestionnaire(QuestionCreateDto model)
        {
            if (model == null || model.Questions == null || model.Questions.Count == 0)
            {
                return BadRequest("Invalid data: Evaluation type and questions are required.");
            }

            try
            {
                // Step A: Header create karein
                var questionnaire = new Questionare
                {
                    type = model.EvaluationType,
                    flag = "0" // Default offline rehta hai create par
                };

                db.Questionare.Add(questionnaire);
                db.SaveChanges(); // 🔥 Yahan ID generate hogi

                // Step B: Questions insert karein loop ke saath (EF 5 compatible)
                foreach (var qText in model.Questions)
                {
                    if (!string.IsNullOrWhiteSpace(qText))
                    {
                        var question = new Questions
                        {
                            QuestionareID = questionnaire.id,
                            QuestionText = qText
                        };
                        db.Questions.Add(question);
                    }
                }

                db.SaveChanges();

                return Ok(new
                {
                    message = "Questionnaire saved successfully",
                    QuestionnaireId = questionnaire.id
                });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return BadRequest("Create Error: " + inner);
            }
        }

        // 2. GET ALL: Dashboard list ke liye
        [HttpGet]
        [Route("GetAll")]
        public IHttpActionResult GetAll()
        {
            var data = db.Questionare
                .Select(q => new QuestionnaireListDto
                {
                    Id = q.id,
                    Type = q.type,
                    Flag = q.flag,
                    QuestionCount = q.Questions.Count()
                })
                .ToList();

            return Ok(data);
        }

        // 3. GET BY ID: Edit page par data load karne ke liye
        [HttpGet]
        [Route("GetById/{id}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var questionnaire = db.Questionare
                    .Include(q => q.Questions)
                    .FirstOrDefault(q => q.id == id);

                if (questionnaire == null) return NotFound();

                var result = new
                {
                    id = questionnaire.id,
                    evaluationType = questionnaire.type,
                    questions = questionnaire.Questions
                        .Select(qq => new
                        {
                            id = qq.QuestionID,
                            questionText = qq.QuestionText
                        })
                        .ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 4. TOGGLE: Active/Inactive status switch karne ke liye
        [HttpPost]
        [Route("Toggle")]
        public IHttpActionResult ToggleQuestionnaire(ToggleQuestionnaireDto model)
        {
            var questionnaire = db.Questionare.Find(model.QuestionnaireId);
            if (questionnaire == null) return NotFound();

            if (model.TurnOn)
            {
                // Check karein ke is type ka koi aur form pehle se Active na ho
                var alreadyActive = db.Questionare.Any(q =>
                    q.type == questionnaire.type &&
                    q.flag == "1" &&
                    q.id != questionnaire.id
                );

                if (alreadyActive)
                    return BadRequest("Another evaluation of this type is already active.");

                questionnaire.flag = "1";
            }
            else
            {
                questionnaire.flag = "0";
            }

            db.SaveChanges();
            return Ok(new { message = "Status updated successfully" });
        }

        // 5. SAVE ALL CHANGES: Edit page par Add, Update aur Delete handle karne ke liye
        [HttpPost]
        [Route("SaveAllChanges")]
        public IHttpActionResult SaveAllChanges(SaveQuestionnaireChangesDto model)
        {
            if (model == null) return BadRequest("Invalid data");

            try
            {
                // A. DELETE: Purane sawalat remove karein
                if (model.DeletedIds != null && model.DeletedIds.Any())
                {
                    foreach (var id in model.DeletedIds)
                    {
                        var qToDelete = db.Questions.Find(id);
                        if (qToDelete != null)
                        {
                            db.Questions.Remove(qToDelete);
                        }
                    }
                }

                // B. ADD & UPDATE:
                if (model.Questions != null)
                {
                    foreach (var q in model.Questions)
                    {
                        if (q.Id == 0) // Naya Question add ho raha hai
                        {
                            if (!string.IsNullOrEmpty(q.QuestionText))
                            {
                                db.Questions.Add(new Questions
                                {
                                    QuestionareID = model.QuestionnaireId,
                                    QuestionText = q.QuestionText
                                });
                            }
                        }
                        else // Purana Question update ho raha hai
                        {
                            var existing = db.Questions.Find(q.Id);
                            if (existing != null)
                            {
                                existing.QuestionText = q.QuestionText;
                                // EF 5 Force Update
                                db.Entry(existing).State = EntityState.Modified;
                            }
                        }
                    }
                }

                db.SaveChanges();
                return Ok(new { message = "Changes saved successfully" });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return BadRequest("Update Error: " + inner);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}