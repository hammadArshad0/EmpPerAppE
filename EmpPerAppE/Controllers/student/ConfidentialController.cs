using EmpPerAppE.Models;
using iTextSharp.text.pdf;
using iTextSharp.text;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Web.Http;
using Newtonsoft.Json;

namespace EmpPerAppE.Controllers.student
{
    [RoutePrefix("api/Confidential")]
    public class ConfidentialController : ApiController
    {
        private fypempEntities db = new fypempEntities();

        public ConfidentialController()
        {
            db.Configuration.ProxyCreationEnabled = false;
            db.Configuration.LazyLoadingEnabled = false;
        }

        [HttpGet]
        [Route("GetStudentEnrollments/{studentId}")]
        public IHttpActionResult GetStudentEnrollments(string studentId)
        {
            try
            {
                var data = db.Enrollment
                    .Where(e => e.studentID == studentId)
                    .Select(e => new
                    {
                        EnrollmentId = e.id,
                        TeacherId = e.teacherID,
                        TeacherName = e.Teacher.name,
                        CourseCode = e.courseCode,
                        CourseTitle = e.Course.title,
                        SessionName = e.Session.name,
                        SessionId = e.sessionID,
                        IsEvaluated = db.StudentEvaluation.Any(se =>
                            se.enrollmentID == e.id &&
                            db.Questions.Any(q => q.QuestionID == se.questionID && q.Questionare.type == "C"))
                    })
                    .ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest("Error: " + ex.Message);
            }
        }

        [HttpPost]
        [Route("SubmitConfidential")]
        public IHttpActionResult SubmitConfidential([FromBody] ConfidentialSubmissionDto model)
        {
            if (model == null) return BadRequest("Model is null. Check JSON format.");
            if (model.Answers == null || !model.Answers.Any()) return BadRequest("Answers list is empty.");

            try
            {
                foreach (var ans in model.Answers)
                {
                    var evaluation = new StudentEvaluation
                    {
                        enrollmentID = model.EnrollmentId,
                        questionID = ans.QuestionId,
                        score = ans.Score
                    };
                    db.StudentEvaluation.Add(evaluation);
                }

                db.SaveChanges();
                return Ok(new { message = "Evaluation saved successfully!" });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
            {
                var errorMessages = dbEx.EntityValidationErrors
                        .SelectMany(x => x.ValidationErrors)
                        .Select(x => x.ErrorMessage);
                var fullErrorMessage = string.Join("; ", errorMessages);
                return BadRequest("Validation Error: " + fullErrorMessage);
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.InnerException?.Message ?? ex.InnerException.Message : ex.Message;
                return BadRequest("Database Error: " + innerMsg);
            }
        }

        //[HttpPost]
        //[Route("GenerateAndEmailReport/{studentId}")]
        //public IHttpActionResult GenerateAndEmailReport(string studentId, [FromBody] EvaluationPayload payload)
        //{
        //    try
        //    {
        //        if (payload == null || payload.Evaluations == null || !payload.Evaluations.Any())
        //            return BadRequest("No confidential evaluation found.");

        //        var activeRecipient = db.Email.FirstOrDefault(x => x.isActive == true);
        //        if (activeRecipient == null || string.IsNullOrEmpty(activeRecipient.mail))
        //            return BadRequest("No active recipient email found.");

        //        string targetEmail = activeRecipient.mail;
        //        var first = payload.Evaluations.First();

        //        // ✅ Yeh format GetEvaluations ke parse logic se match karta hai
        //        var emailData = new
        //        {
        //            studentId = first.StudentId,
        //            studentName = first.StudentName,
        //            teacherId = first.TeacherId,
        //            teacherName = first.TeacherName,
        //            session = first.SessionName,
        //            subjectCode = first.CourseCode,
        //            submittedOn = DateTime.Now,
        //            evaluation = payload.Evaluations.Select(e => new
        //            {
        //                questionText = e.QuestionText,
        //                score = e.Score
        //            }).ToList()
        //        };

        //        string jsonReport = JsonConvert.SerializeObject(emailData, Formatting.Indented);
        //        SendJsonEmail(jsonReport, targetEmail, studentId);

        //        return Ok(new { message = $"Report sent to {targetEmail}!" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(new Exception("System Error: " + ex.Message));
        //    }
        //}

        //private void SendJsonEmail(string jsonContent, string recipientEmail, string sid)
        //{
        //    // ✅ Pehle payload ko re-serialize karo flat format mein
        //    // jsonContent jo aa rahi hai wo grouped format mein hai
        //    // Hum use directly bhej dete hain lekin markers ke saath

        //    var fromEmail = new MailAddress("hammadarshad88888@gmail.com", "Employee Performance");
        //    var toEmail = new MailAddress(recipientEmail);
        //    string appPassword = "umxziyrjnmpparos";

        //    var smtp = new SmtpClient
        //    {
        //        Host = "smtp.gmail.com",
        //        Port = 587,
        //        EnableSsl = true,
        //        DeliveryMethod = SmtpDeliveryMethod.Network,
        //        UseDefaultCredentials = false,
        //        Credentials = new NetworkCredential(fromEmail.Address, appPassword)
        //    };

        //    using (var mail = new MailMessage(fromEmail, toEmail))
        //    {
        //        mail.Subject = $"Confidential Evaluation - {sid}";

        //        // ✅ START_EVAL aur END_EVAL markers zaroori hain
        //        mail.Body = $"Confidential Evaluation Report\n\nSTART_EVAL\n{jsonContent}\nEND_EVAL\n\nRegards,\nFYP System";
        //        mail.IsBodyHtml = false;
        //        smtp.Send(mail);
        //    }
        //}

        [HttpPost]
        [Route("GenerateAndEmailReport/{studentId}")]
        public IHttpActionResult GenerateAndEmailReport(string studentId, [FromBody] EvaluationPayload payload)
        {
            try
            {
                if (payload == null || payload.Evaluations == null || !payload.Evaluations.Any())
                    return BadRequest("No confidential evaluation found.");

                // ── 1. Database se Active Email uthayein ──────────────────────────────
                // 'Emails' ya 'Email' (jo bhi aapke EDMX mein table ka naam hai)
                var activeRecipient = db.Email.FirstOrDefault(x => x.isActive == true);

                if (activeRecipient == null || string.IsNullOrEmpty(activeRecipient.mail))
                {
                    return BadRequest("No active recipient email found in settings. Please contact Director.");
                }

                string targetEmail = activeRecipient.mail;

                // Student info pehli record se lo
                var first = payload.Evaluations.First();

                // ── 2. Teacher ke basis par group karo ──────────────────────────────
                var groupedByTeacher = payload.Evaluations
                    .GroupBy(e => new { e.TeacherId, e.TeacherName, e.CourseCode, e.CourseTitle })
                    .Select(g => new
                    {
                        Teacher = new { ID = g.Key.TeacherId, Name = g.Key.TeacherName },
                        Course = new { Code = g.Key.CourseCode, Title = g.Key.CourseTitle },
                        Evaluations = g.Select(e => new
                        {
                            QuestionID = e.QuestionId,
                            Question = e.QuestionText,
                            Score = e.Score
                        }).ToList()
                    }).ToList();

                // ── 3. Final structured report ───────────────────────────────────────
                var finalReport = new
                {
                    ReportTitle = "CONFIDENTIAL EVALUATION REPORT",
                    Date = DateTime.Now.ToString("dd-MMM-yyyy"),
                    Student = new { ID = first.StudentId, Name = first.StudentName },
                    Session = first.SessionName,
                    TeacherEvaluations = groupedByTeacher
                };

                string jsonReport = JsonConvert.SerializeObject(finalReport, Formatting.Indented);

                // ── 4. Target Email ab database se aa rahi hai ────────────────────────
                SendJsonEmail(jsonReport, targetEmail, studentId);

                return Ok(new { message = $"Grouped JSON Report sent to {targetEmail}!" });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("System Error: " + ex.Message));
            }
        }
        //1. Iska naam SendJsonEmail hona chahiye
        //2. Iska pehla parameter string hona chahiye(byte[] nahi)
        private void SendJsonEmail(string jsonContent, string recipientEmail, string sid)
        {
            var fromEmail = new MailAddress("hammadarshad88888@gmail.com", "Employee Performance Confidential report");
            var toEmail = new MailAddress(recipientEmail);
            string appPassword = "umxziyrjnmpparos";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromEmail.Address, appPassword)
            };

            using (var mail = new MailMessage(fromEmail, toEmail))
            {
                // ✅ Subject mein "Confidential Evaluation" hona zaroori hai
                mail.Subject = $"Confidential Evaluation - {sid}";

                // ✅ START_EVAL aur END_EVAL markers add kiye
                mail.Body = $"Confidential Evaluation Report\n\nSTART_EVAL\n{jsonContent}\nEND_EVAL\n\nRegards,\nFYP System";
                mail.IsBodyHtml = false;
                smtp.Send(mail);
            }
        }
    }

    public class ConfidentialSubmissionDto
    {
        public int EnrollmentId { get; set; }
        public List<ConfidentialAnswerDto> Answers { get; set; }
    }

    public class ConfidentialAnswerDto
    {
        public int QuestionId { get; set; }
        public int Score { get; set; }
    }

    //email dto
    public class EvaluationPayload
    {
        public List<EvaluationItem> Evaluations { get; set; }
    }

    public class EvaluationItem
    {
        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string TeacherId { get; set; }
        public string TeacherName { get; set; }
        public string CourseCode { get; set; }
        public string CourseTitle { get; set; }
        public string SessionName { get; set; }
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public int Score { get; set; }
    }
}