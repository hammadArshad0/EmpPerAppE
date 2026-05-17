using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Xml;
using EmpPerAppE.Models;
using EmpPerAppE.Models.DTO; // Standard namespace use karein

namespace EmpPerAppE.Controllers.task
{
    [RoutePrefix("api/ProjectEvaluation")]
    public class ProjectEvaluationController : ApiController
    {
        fypempEntities db = new fypempEntities();

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
        } // Method end

        // 1. Get All Teachers (to toggle committee status)
        [HttpGet]
        [Route("GetAllTeachers")]
        public IHttpActionResult GetAllTeachers()
        {
            var teachers = db.Teacher.Select(t => new
            {
                t.userID,
                t.name,
                t.department,
                t.IsCommitteeMember
            }).ToList();
            return Ok(teachers);
        }

        // 2. Update Committee Status
        [HttpPost]
        [Route("UpdateCommitteeStatus")]
        public IHttpActionResult UpdateCommitteeStatus(ProjectDTOs.CommitteeStatusDTO dto)
        {
            var teacher = db.Teacher.Find(dto.UserID);
            if (teacher == null) return NotFound();

            teacher.IsCommitteeMember = dto.IsMember;
            db.SaveChanges();
            return Ok(new { Message = "Status updated successfully" });
        }

        // 3. Get Only Committee Members (For Evaluation Screen)
        [HttpGet]
        [Route("GetCommitteeMembers")]
        public IHttpActionResult GetCommitteeMembers()
        {
            var members = db.Teacher
                .Where(t => t.IsCommitteeMember == true)
                .Select(t => new { t.userID, t.name, t.department })
                .ToList();
            return Ok(members);
        }

        // 4. Save Final Evaluation
        [HttpPost]
        [Route("SaveProjectEvaluation")]
        public IHttpActionResult SaveProjectEvaluation(ProjectDTOs.ProjectEvaluationRequest dto)
        {
            if (dto == null || dto.Evaluations == null) return BadRequest("Data missing");

            try
            {
                // ✅ Har request par latest session ID yahan se nikalni hai
                var latestSessionId = db.Session
                                        .OrderByDescending(s => s.id)
                                        .Select(s => s.id)
                                        .FirstOrDefault();

                // Check karein agar session exist nahi karta
                if (latestSessionId == 0) return BadRequest("No active session found in database.");

                foreach (var item in dto.Evaluations)
                {
                    var eval = new ProjectEvaluation
                    {
                        evaluatorID = dto.EvaluatorId,
                        evaluateeID = dto.EvaluateeId,
                        questionID = item.QuestionId,
                        score = item.Score,
                        sessionID = latestSessionId // ✅ DTO wala ID use karne ke bajaye latest variable use karein
                    };
                    db.ProjectEvaluation.Add(eval);
                }

                db.SaveChanges();
                return Ok("Evaluation saved successfully!");
            }
            catch (Exception ex)
            {
                // Inner exception check karne ke liye isse debug mein dekhein
                return InternalServerError(ex);
            }
        }
    }
}