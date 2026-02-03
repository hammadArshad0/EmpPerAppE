using EmpPerAppE.Models;
using ExcelDataReader;
using System;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace EmpPerAppE.Controllers.enrollment
{
    [RoutePrefix("api/Enrollment")]
    public class EnrollmentController : ApiController
    {
        fypempEntities db = new fypempEntities();

        [HttpPost]
        [Route("UploadExcel")]
        public IHttpActionResult UploadEnrollment()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

             
                if (string.IsNullOrEmpty(httpRequest.Form["sessionId"]))
                    return BadRequest("Session not selected.");

                int sessionId = int.Parse(httpRequest.Form["sessionId"]);

                if (db.Session.Find(sessionId) == null)
                    return BadRequest("Invalid session.");

              
                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];
                if (file == null || file.ContentLength == 0)
                    return BadRequest("Empty file.");

                System.Text.Encoding.RegisterProvider(
                    System.Text.CodePagesEncodingProvider.Instance);

                int inserted = 0;
                int skippedDuplicate = 0;

                using (var stream = file.InputStream)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                        {
                            UseHeaderRow = true
                        }
                    });

                    var table = result.Tables[0];

                    foreach (DataRow row in table.Rows)
                    {
                        string studentID = row["StudentID"]?.ToString().Trim();
                        string teacherID = row["TeacherID"]?.ToString().Trim();
                        string courseCode = row["CourseCode"]?.ToString().Trim();

                      
                        if (string.IsNullOrEmpty(studentID) ||
                            string.IsNullOrEmpty(teacherID) ||
                            string.IsNullOrEmpty(courseCode))
                            continue;

                       
                        bool exists = db.Enrollment.Any(e =>
                            e.studentID == studentID &&
                            e.courseCode == courseCode &&
                            e.sessionID == sessionId
                        );

                        if (exists)
                        {
                            skippedDuplicate++;
                            continue;
                        }

                        db.Enrollment.Add(new Enrollment
                        {
                            studentID = studentID,
                            teacherID = teacherID,
                            courseCode = courseCode,
                            sessionID = sessionId
                        });

                        inserted++;
                    }

                    db.SaveChanges();
                }

                return Ok($"{inserted} enrollment uploaded successfully. {skippedDuplicate} duplicates skipped.");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}
