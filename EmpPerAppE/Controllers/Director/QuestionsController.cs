using EmpPerAppE.Models;
using EmpPerAppE.Models.DTO;
using System;
using System.Linq;
using System.Web.Http;
using System.Data.Entity;
using System.Collections.Generic;

namespace EmpPerAppE.Controllers.Director
{
    [RoutePrefix("api/Questions")]
    public class QuestionsController : ApiController
    {
        private fypempEntities db = new fypempEntities();

        public QuestionsController()
        {
            db.Configuration.ProxyCreationEnabled = false;
            db.Configuration.LazyLoadingEnabled = false;
        }

        // ================================
        // 1️⃣ CREATE QUESTIONNAIRE
        // ================================
        [HttpPost]
        [Route("Create")]
        public IHttpActionResult CreateQuestionnaire(QuestionCreateDto model)
        {
            if (model == null || model.Questions == null || model.Questions.Count == 0)
                return BadRequest("Evaluation type and questions are required.");

            try
            {
                // Create Header
                var questionnaire = new Questionare
                {
                    type = model.EvaluationType,
                    flag = "0"
                };

                db.Questionare.Add(questionnaire);
                db.SaveChanges(); // ID generated

                // Insert Questions
                foreach (var qText in model.Questions)
                {
                    if (!string.IsNullOrWhiteSpace(qText))
                    {
                        db.Questions.Add(new Questions
                        {
                            QuestionareID = questionnaire.id,
                            QuestionText = qText
                        });
                    }
                }

                db.SaveChanges();

                return Ok(new
                {
                    message = "Questionnaire created successfully",
                    QuestionnaireId = questionnaire.id
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ================================
        // 2️⃣ GET ALL QUESTIONNAIRES
        // ================================
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

        // ================================
        // 3️⃣ GET BY ID
        // ================================
        [HttpGet]
        [Route("GetById/{id}")]
        public IHttpActionResult GetById(int id)
        {
            var questionnaire = db.Questionare
                .Include(q => q.Questions)
                .FirstOrDefault(q => q.id == id);

            if (questionnaire == null)
                return NotFound();

            var result = new
            {
                id = questionnaire.id,
                evaluationType = questionnaire.type,
                questions = questionnaire.Questions
                    .Select(q => new
                    {
                        id = q.QuestionID,
                        questionText = q.QuestionText
                    }).ToList()
            };

            return Ok(result);
        }

        // ================================
        // 4️⃣ TOGGLE ACTIVE / INACTIVE
        // ================================
        [HttpPost]
        [Route("Toggle")]
        public IHttpActionResult ToggleQuestionnaire(ToggleQuestionnaireDto model)
        {
            var questionnaire = db.Questionare.Find(model.QuestionnaireId);
            if (questionnaire == null)
                return NotFound();

            if (model.TurnOn)
            {
                bool alreadyActive = db.Questionare.Any(q =>
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

        // ================================
        // 5️⃣ SAVE ALL CHANGES (EDIT PAGE)
        // ================================
        [HttpPost]
        [Route("SaveAllChanges")]
        public IHttpActionResult SaveAllChanges(SaveQuestionnaireChangesDto model)
        {
            if (model == null)
                return BadRequest("Invalid data");

            try
            {
                // DELETE
                if (model.DeletedIds != null && model.DeletedIds.Any())
                {
                    foreach (var id in model.DeletedIds)
                    {
                        var q = db.Questions.Find(id);
                        if (q != null)
                            db.Questions.Remove(q);
                    }
                }

                // ADD & UPDATE
                if (model.Questions != null)
                {
                    foreach (var q in model.Questions)
                    {
                        if (q.Id == 0) // NEW
                        {
                            if (!string.IsNullOrWhiteSpace(q.QuestionText))
                            {
                                db.Questions.Add(new Questions
                                {
                                    QuestionareID = model.QuestionnaireId,
                                    QuestionText = q.QuestionText
                                });
                            }
                        }
                        else // UPDATE
                        {
                            var existing = db.Questions.Find(q.Id);
                            if (existing != null)
                            {
                                existing.QuestionText = q.QuestionText;
                            }
                        }
                    }
                }

                db.SaveChanges();
                return Ok(new { message = "Changes saved successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }
        // 2. Get Questionnaire (Type 'P' or 'T' from frontend)
        [HttpGet]
        [Route("GetActiveQuestionnaire/{type}")]
        public IHttpActionResult GetActiveQuestionnaire(string type)
        {
            try
            {
                var questionnaire = db.Questionare
                    .Where(q => q.flag == "1" && q.type.Trim().ToLower() == type.Trim().ToLower())
                    .Select(q => new
                    {
                        QuestionareID = q.id,
                        Questions = db.Questions
                            .Where(ques => ques.QuestionareID == q.id)
                            .Select(ques => new { ques.QuestionID, ques.QuestionText })
                            .ToList()
                    }).FirstOrDefault();

                if (questionnaire == null) return Ok(new { Message = "No active questionnaire found" });
                return Ok(questionnaire);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // ================================
        // 6️⃣ DELETE FULL QUESTIONNAIRE
        // ================================
        [HttpDelete]
        [Route("Delete/{id}")]
        public IHttpActionResult DeleteQuestionnaire(int id)
        {
            try
            {
                var questionnaire = db.Questionare.Include(q => q.Questions).FirstOrDefault(q => q.id == id);

                if (questionnaire == null)
                    return NotFound();

                // Pehle iske saare sawal delete karein
                if (questionnaire.Questions != null && questionnaire.Questions.Any())
                {
                    db.Questions.RemoveRange(questionnaire.Questions);
                }

                // Phir main header delete karein
                db.Questionare.Remove(questionnaire);
                db.SaveChanges();

                return Ok(new { message = "Questionnaire and all its questions deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest("Cannot delete: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        // ================================
        // 7️⃣ DELETE INDIVIDUAL QUESTION (Optional - For Edit Page)
        // ================================
        [HttpDelete]
        [Route("DeleteQuestion/{id}")]
        public IHttpActionResult DeleteIndividualQuestion(int id)
        {
            try
            {
                var question = db.Questions.Find(id);
                if (question == null)
                    return NotFound();

                db.Questions.Remove(question);
                db.SaveChanges();

                return Ok(new { message = "Question removed successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest("Error deleting question: " + ex.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}
