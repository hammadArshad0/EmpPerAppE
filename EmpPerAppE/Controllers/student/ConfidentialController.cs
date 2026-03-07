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
        //public IHttpActionResult GenerateAndEmailReport(string studentId)
        //{
        //    try
        //    {
        //        // Student name aur session fetch karo
        //        var studentInfo = db.Enrollment
        //            .Where(e => e.studentID == studentId)
        //            .Select(e => new {
        //                StudentName = e.Student.name,
        //                SessionName = e.Session.name
        //            })
        //            .FirstOrDefault();

        //        string studentName = studentInfo?.StudentName ?? "N/A";
        //        string sessionName = studentInfo?.SessionName ?? "N/A";

        //        var results = db.StudentEvaluation
        //            .Where(se => se.Enrollment.studentID == studentId && se.Questions.Questionare.type == "C")
        //            .Select(se => new {
        //                Teacher = se.Enrollment.Teacher.name,
        //                Course = se.Enrollment.Course.title,
        //                Question = se.Questions.QuestionText,
        //                Score = se.score
        //            }).ToList();

        //        if (!results.Any()) return BadRequest("Koi data nahi mila.");

        //        using (MemoryStream ms = new MemoryStream())
        //        {
        //            Document document = new Document(PageSize.A4, 25, 25, 30, 30);
        //            PdfWriter writer = PdfWriter.GetInstance(document, ms);
        //            document.Open();

        //            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLACK);
        //            var labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.BLACK);
        //            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
        //            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);

        //            // Title
        //            Paragraph title = new Paragraph("CONFIDENTIAL EVALUATION REPORT", titleFont);
        //            title.Alignment = Element.ALIGN_CENTER;
        //            document.Add(title);
        //            document.Add(new Paragraph("\n"));

        //            // Student Info
        //            document.Add(new Paragraph($"Student ID:   {studentId}", labelFont));
        //            document.Add(new Paragraph($"Student Name: {studentName}", labelFont));
        //            document.Add(new Paragraph($"Session:      {sessionName}", labelFont));
        //            document.Add(new Paragraph($"Date:         {DateTime.Now:dd-MMM-yyyy}", labelFont));
        //            document.Add(new Paragraph("\n"));

        //            // Table
        //            PdfPTable table = new PdfPTable(4);
        //            table.WidthPercentage = 100;
        //            table.SetWidths(new float[] { 20f, 20f, 50f, 10f });

        //            string[] headers = { "Teacher", "Course", "Question", "Score" };
        //            foreach (var h in headers)
        //            {
        //                PdfPCell cell = new PdfPCell(new Phrase(h, headerFont));
        //                cell.BackgroundColor = new BaseColor(40, 40, 40);
        //                cell.HorizontalAlignment = Element.ALIGN_CENTER;
        //                cell.Padding = 5;
        //                table.AddCell(cell);
        //            }

        //            foreach (var item in results)
        //            {
        //                table.AddCell(new Phrase(item.Teacher, normalFont));
        //                table.AddCell(new Phrase(item.Course, normalFont));
        //                table.AddCell(new Phrase(item.Question, normalFont));
        //                PdfPCell sCell = new PdfPCell(new Phrase(item.Score.ToString(), normalFont));
        //                sCell.HorizontalAlignment = Element.ALIGN_CENTER;
        //                table.AddCell(sCell);
        //            }

        //            document.Add(table);
        //            document.Close();

        //            byte[] pdfBytes = ms.ToArray();
        //            SendEmail(pdfBytes, "hammadarshad481@gmail.com", studentId, studentName, sessionName);
        //        }

        //        return Ok(new { message = "Report sent to admin!" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(new Exception("System Error: " + ex.Message));
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
        // 1. Iska naam SendJsonEmail hona chahiye
        // 2. Iska pehla parameter string hona chahiye (byte[] nahi)
        private void SendJsonEmail(string jsonContent, string recipientEmail, string sid)
        {
            var fromEmail = new MailAddress("hammadarshad88888@gmail.com", "Employee Performance Confidential  report");
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
                mail.Subject = $"Confidential JSON Evaluation Report - {sid}";

                // Body mein JSON text ja raha hai
                mail.Body = $" \n\nPlease view  the confidential evaluation report in JSON format below:\n\n{jsonContent}\n\nRegards,\nFYP System";

                mail.IsBodyHtml = false; // Plain text JSON ke liye behtar hai

                // Attachments ki zaroorat nahi hai kyunke hum body mein bhej rahe hain
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