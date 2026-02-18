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

        // 1. GET: Enrollments fetch karein
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
            // Check 1: Model null toh nahi?
            if (model == null) return BadRequest("Model is null. Check JSON format.");

            // Check 2: Answers list empty toh nahi?
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
                // Database validation errors check karne ke liye
                var errorMessages = dbEx.EntityValidationErrors
                        .SelectMany(x => x.ValidationErrors)
                        .Select(x => x.ErrorMessage);
                var fullErrorMessage = string.Join("; ", errorMessages);
                return BadRequest("Validation Error: " + fullErrorMessage);
            }
            catch (Exception ex)
            {
                // Inner Exception check karein (Foreign Key errors ke liye)
                var innerMsg = ex.InnerException != null ? ex.InnerException.InnerException?.Message ?? ex.InnerException.Message : ex.Message;
                return BadRequest("Database Error: " + innerMsg);
            }
        }
        // 3. POST: Report Generate aur Email karein
        [HttpPost]
        [Route("GenerateAndEmailReport/{studentId}")]
        public IHttpActionResult GenerateAndEmailReport(string studentId)
        {
            try
            {
                var results = db.StudentEvaluation
                    .Where(se => se.Enrollment.studentID == studentId && se.Questions.Questionare.type == "C")
                    .Select(se => new {
                        Teacher = se.Enrollment.Teacher.name,
                        Course = se.Enrollment.Course.title,
                        Question = se.Questions.QuestionText,
                        Score = se.score
                    }).ToList();

                if (!results.Any()) return BadRequest("Koi data nahi mila.");

                using (MemoryStream ms = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 25, 25, 30, 30);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLACK);
                    var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);
                    var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

                    Paragraph title = new Paragraph("MANAGEMENT EVALUATION REPORT", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    document.Add(title);
                    document.Add(new Paragraph("\n"));
                    document.Add(new Paragraph($"Student ID: {studentId}"));
                    document.Add(new Paragraph($"Date: {DateTime.Now:dd-MMM-yyyy}"));
                    document.Add(new Paragraph("\n"));

                    PdfPTable table = new PdfPTable(4);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 20f, 20f, 50f, 10f });

                    string[] headers = { "Teacher", "Course", "Question", "Score" };
                    foreach (var h in headers)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(h, headerFont));
                        cell.BackgroundColor = new BaseColor(40, 40, 40);
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.Padding = 5;
                        table.AddCell(cell);
                    }

                    foreach (var item in results)
                    {
                        table.AddCell(new Phrase(item.Teacher, normalFont));
                        table.AddCell(new Phrase(item.Course, normalFont));
                        table.AddCell(new Phrase(item.Question, normalFont));
                        PdfPCell sCell = new PdfPCell(new Phrase(item.Score.ToString(), normalFont));
                        sCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        table.AddCell(sCell);
                    }

                    document.Add(table);
                    document.Close();

                    byte[] pdfBytes = ms.ToArray();
                    SendEmail(pdfBytes, "hammadarshad481@gmail.com", studentId);
                }

                return Ok(new { message = "Report sent to admin!" });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("System Error: " + ex.Message));
            }
        }

        private void SendEmail(byte[] pdfData, string recipientEmail, string sid)
        {
            var fromEmail = new MailAddress("hammadarshad88888@gmail.com", "FYP Admin");
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
                mail.Subject = $"Confidential Report - {sid}";
                mail.Body = $"Attached is the evaluation report for Student ID: {sid}.";
                mail.Attachments.Add(new Attachment(new MemoryStream(pdfData), $"Report_{sid}.pdf"));
                smtp.Send(mail);
            }
        }
    }

    // --- DTOs (Sirf ek baar namespace ke andar define karein) ---
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
}