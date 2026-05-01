using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using EmpPerAppE.Models;
using EmpPerAppE.Models.DTO;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/SocietyEvaluation")]
    public class SocietyEvaluationController : ApiController
    {
        fypempEntities db = new fypempEntities();

        [HttpPost]
        [Route("SubmitSEM")]
        public IHttpActionResult SubmitSocietyEvaluationSEM([FromBody] List<SocietyEvaluationDTO> evaluations)
        {
            if (evaluations == null || !evaluations.Any())
                return BadRequest("Invalid submission");

            try
            {
                var latestSession = db.Session
                    .OrderByDescending(s => s.id)
                    .FirstOrDefault();

                if (latestSession == null)
                    return BadRequest("No active session");

                foreach (var e in evaluations)
                {
                    //  Prevent duplicate (VERY IMPORTANT)
                    var exists = db.SocietyEvaluation.Any(x =>
                        x.EvaluatorId == e.EvaluatorId &&
                        x.EvaluateeId == e.EvaluateeId &&
                        x.SocietyId == e.SocietyId &&
                        x.QuestionId == e.QuestionId &&
                        x.SessionId == latestSession.id &&
                        x.EvaluationType.Trim().ToLower() == e.EvaluationType.Trim().ToLower()
                    );

                    if (!exists)
                    {
                        db.SocietyEvaluation.Add(new SocietyEvaluation
                        {
                            EvaluatorId = e.EvaluatorId,
                            EvaluateeId = e.EvaluateeId,
                            SocietyId = e.SocietyId,
                            QuestionId = e.QuestionId,
                            Score = e.Score,
                            SessionId = latestSession.id,
                            EvaluationType = e.EvaluationType
                        });
                    }
                }

                db.SaveChanges();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        /// chair personn ke liye update logic ke sath submit function
        [HttpPost]
        [Route("Submit")]
        public IHttpActionResult SubmitSocietyEvaluation([FromBody] List<SocietyEvaluationDTO> evaluations)
        {
            if (evaluations == null || !evaluations.Any())
                return BadRequest("No evaluation data received");

            try
            {
                foreach (var e in evaluations)
                {
                    // Normalize Input
                    var evaluatorId = e.EvaluatorId?.Trim();
                    var evaluateeId = e.EvaluateeId?.Trim();
                    var evaluationType = e.EvaluationType?.Trim();

                    // 1. Validation Checks (Database integrity ke liye zaroori hain)
                    if (!db.Teacher.Any(t => t.userID == evaluatorId) || !db.Teacher.Any(t => t.userID == evaluateeId))
                        return Ok(new { success = false, error = $"Invalid Teacher IDs for {e.EvaluateeId}" });

                    if (!db.Session.Any(s => s.id == e.SessionId))
                        return Ok(new { success = false, error = "Invalid SessionId" });

                    // 2. DUPLICATE CHECK & UPDATE LOGIC
                    var existingRecord = db.SocietyEvaluation.FirstOrDefault(x =>
                     x.EvaluatorId == evaluatorId &&
                      x.EvaluateeId == evaluateeId &&
                     x.SocietyId == e.SocietyId &&
                    x.QuestionId == e.QuestionId &&
                     x.SessionId == e.SessionId &&
                   x.EvaluationType == evaluationType
                       );

                    if (existingRecord != null)
                    {
                        // Agar record pehle se hai, to naya Score update kar do
                        // Agar record pehle se hai, to naya Score update kar do
                        existingRecord.Score = e.Score;
                    }
                    else
                    {
                        // Agar record nahi hai, to naya Add karo
                        db.SocietyEvaluation.Add(new SocietyEvaluation
                        {
                            EvaluatorId = evaluatorId,
                            EvaluateeId = evaluateeId,
                            SocietyId = e.SocietyId,
                            QuestionId = e.QuestionId,
                            Score = e.Score,
                            SessionId = e.SessionId,
                            EvaluationType = evaluationType

                        });
                    }
                }

                db.SaveChanges();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    inner = ex.InnerException?.InnerException?.Message
                });
            }
        }


        [HttpGet]
        [Route("GetSubmitted/{evaluatorId}/{evaluationType}/{sessionId}")]
        public IHttpActionResult GetSubmittedEvaluations(string evaluatorId, string evaluationType, int sessionId)
        {

            var submitted = db.SocietyEvaluation
                .Where(x =>
                    x.EvaluatorId.Trim().ToLower() == evaluatorId.Trim().ToLower() &&
                    x.SessionId == sessionId &&
                    x.EvaluationType == evaluationType
                )
                .Select(x => x.EvaluateeId)
                .Distinct()
                .ToList();

            return Ok(submitted);
        }

        //[HttpGet]
        //[Route("GetChairpersonSocietyWithMentors/{teacherId}/{sessionId}")]
        //public IHttpActionResult GetChairpersonSocietyWithMentors(string teacherId, int sessionId)
        //{
        //    // 🔹 Find society where this teacher is chairperson
        //    var society = db.SocietyAssignments
        //        .Where(x => x.TeacherId == teacherId &&
        //                    x.SessionId == sessionId &&
        //                    x.IsChairperson == true)
        //        .Select(x => new
        //        {
        //            x.SocietyId,
        //            SocietyName = x.Societies.SocietyName
        //        })
        //        .FirstOrDefault();

        //    // ❌ Not a chairperson
        //    if (society == null)
        //    {
        //        return Ok(new
        //        {
        //            IsChairperson = false
        //        });
        //    }

        //    // 🔹 Get mentors of that society
        //    var mentors = db.SocietyAssignments
        //        .Where(x => x.SocietyId == society.SocietyId &&
        //                    x.SessionId == sessionId &&
        //                    x.IsMentor == true)
        //        .Join(db.Teacher,
        //              a => a.TeacherId,
        //              t => t.userID,
        //              (a, t) => new
        //              {
        //                  TeacherId = t.userID,
        //                  TeacherName = t.name,
        //                  SocietyId = a.SocietyId,
        //                  SocietyName = society.SocietyName
        //              })
        //        .ToList();

        //    return Ok(new
        //    {
        //        IsChairperson = true,
        //        SocietyId = society.SocietyId,
        //        SocietyName = society.SocietyName,
        //        Mentors = mentors
        //    });
        //}
        [HttpGet]
        [Route("GetChairpersonSocietyWithMentors/{teacherId}/{sessionId}")]
        public IHttpActionResult GetChairpersonSocietyWithMentors(string teacherId, int sessionId)
        {
            // 1. Un tamam societies ki list nikalen jahan ye teacher Chairperson hai
            var societiesAsChair = db.SocietyAssignments
                .Where(x => x.TeacherId == teacherId &&
                            x.SessionId == sessionId &&
                            x.IsChairperson == true)
                .Select(x => new
                {
                    x.SocietyId,
                    SocietyName = x.Societies.SocietyName
                })
                .ToList(); // FirstOrDefault ki jagah ToList use karein

            // 2. Agar kahin bhi chairperson nahi hai
            if (societiesAsChair == null || !societiesAsChair.Any())
            {
                return Ok(new { IsChairperson = false });
            }

            // 3. In tamam societies ke mentors nikalen
            // Hum har society ke liye uske mentors fetch karenge
            var result = societiesAsChair.Select(s => new
            {
                IsChairperson = true,
                s.SocietyId,
                s.SocietyName,
                Mentors = db.SocietyAssignments
                    .Where(x => x.SocietyId == s.SocietyId &&
                                x.SessionId == sessionId &&
                                x.IsMentor == true)
                    .Join(db.Teacher,
                          a => a.TeacherId,
                          t => t.userID,
                          (a, t) => new
                          {
                              TeacherId = t.userID,
                              TeacherName = t.name,
                              SocietyId = a.SocietyId,
                              SocietyName = s.SocietyName
                          })
                    .ToList()
            }).ToList();

            return Ok(result); // Ab ye array of societies return karega
        }

        [HttpGet]
        [Route("GetChairpersons/{sessionId}")]
        public IHttpActionResult GetChairpersons(int sessionId)
        {
            var data = db.SocietyAssignments
                .Where(a => a.SessionId == sessionId
                         && a.IsChairperson == true)
                .Join(db.Teacher,
                      a => a.TeacherId,
                      t => t.userID,
                      (a, t) => new
                      {
                          SocietyId = a.SocietyId,
                          SocietyName = a.Societies.SocietyName,
                          SessionId = a.SessionId,
                          TeacherId = a.TeacherId,
                          TeacherName = t.name
                      })
                .ToList(); // 👈 ONLY ONE CHAIRPERSON

            return Ok(data);
        }

        // 2. GET: Active Questionnaire (FIXED WITH .INCLUDE)
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


    }
}
