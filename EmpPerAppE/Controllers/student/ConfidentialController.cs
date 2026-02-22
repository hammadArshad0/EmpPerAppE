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

        [HttpPost]
        [Route("GenerateAndEmailReport/{studentId}")]
        public IHttpActionResult GenerateAndEmailReport(string studentId)
        {
            try
            {
                // Student name aur session fetch karo
                var studentInfo = db.Enrollment
                    .Where(e => e.studentID == studentId)
                    .Select(e => new {
                        StudentName = e.Student.name,
                        SessionName = e.Session.name
                    })
                    .FirstOrDefault();

                string studentName = studentInfo?.StudentName ?? "N/A";
                string sessionName = studentInfo?.SessionName ?? "N/A";

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
                    var labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.BLACK);
                    var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                    var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);

                    // Title
                    Paragraph title = new Paragraph("CONFIDENTIAL EVALUATION REPORT", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    document.Add(title);
                    document.Add(new Paragraph("\n"));

                    // Student Info
                    document.Add(new Paragraph($"Student ID:   {studentId}", labelFont));
                    document.Add(new Paragraph($"Student Name: {studentName}", labelFont));
                    document.Add(new Paragraph($"Session:      {sessionName}", labelFont));
                    document.Add(new Paragraph($"Date:         {DateTime.Now:dd-MMM-yyyy}", labelFont));
                    document.Add(new Paragraph("\n"));

                    // Table
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
                    SendEmail(pdfBytes, "hammadarshad481@gmail.com", studentId, studentName, sessionName);
                }

                return Ok(new { message = "Report sent to admin!" });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("System Error: " + ex.Message));
            }
        }

        private void SendEmail(byte[] pdfData, string recipientEmail, string sid, string studentName, string sessionName)
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
                mail.Subject = $"Confidential Evaluation Report - {sid}";
                mail.Body = $"Dear Admin,\n\nPlease find attached the confidential evaluation report.\n\nStudent ID:   {sid}\nStudent Name: {studentName}\nSession:      {sessionName}\nDate:         {DateTime.Now:dd-MMM-yyyy}\n\nRegards,\nFYP System";
                mail.Attachments.Add(new Attachment(new MemoryStream(pdfData), $"ConfidentialReport_{sid}.pdf"));
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
}