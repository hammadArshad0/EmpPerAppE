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


        //[HttpPost]
        //[Route("UploadExcel")]
        //public IHttpActionResult UploadEnrollment()
        //{
        //    try             
        //    {
        //        var httpRequest = HttpContext.Current.Request;

        //        if (string.IsNullOrEmpty(httpRequest.Form["sessionId"]))
        //            return BadRequest("Session not selected.");

        //        int sessionId = int.Parse(httpRequest.Form["sessionId"]);

        //        if (db.Session.Find(sessionId) == null)
        //            return BadRequest("Invalid session.");

        //        if (httpRequest.Files.Count == 0)
        //            return BadRequest("No file uploaded.");

        //        var file = httpRequest.Files[0];
        //        if (file == null || file.ContentLength == 0)
        //            return BadRequest("Empty file.");

        //        System.Text.Encoding.RegisterProvider(
        //            System.Text.CodePagesEncodingProvider.Instance);

        //        int inserted = 0;
        //        int skippedDuplicate = 0;

        //        using (var stream = file.InputStream)
        //        using (var reader = ExcelReaderFactory.CreateReader(stream))
        //        {
        //            var result = reader.AsDataSet(new ExcelDataSetConfiguration()
        //            {
        //                ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
        //                {
        //                    UseHeaderRow = true
        //                }
        //            });

        //            var table = result.Tables[0];

        //            foreach (DataRow row in table.Rows)
        //            {
        //                string studentID = row["studentID"]?.ToString().Trim(); // Note: Check case sensitivity
        //                string teacherID = row["teacherID"]?.ToString().Trim();
        //                string courseCode = row["courseCode"]?.ToString().Trim();

        //                // 🟢 NEW CHANGE: Section aur Grade read karein
        //                //string section = table.Columns.Contains("Section") ? row["Section"]?.ToString().Trim() : null;
        //                //string grade = table.Columns.Contains("Grade") ? row["Grade"]?.ToString().Trim() : null;

        //                // Purana tareeqa (Case sensitive):
        //                // string section = table.Columns.Contains("Section") ? row["Section"]?.ToString().Trim() : null;

        //                // Naya aur Safe tareeqa:
        //                string section = null;
        //                string grade = null;

        //                // Ye code check karega ke "section" naam ka column hai, chahe capital ho ya small
        //                var sectionCol = table.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.Equals("Section", StringComparison.OrdinalIgnoreCase));
        //                var gradeCol = table.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.Equals("Grade", StringComparison.OrdinalIgnoreCase));

        //                if (sectionCol != null) section = row[sectionCol].ToString().Trim();
        //                if (gradeCol != null) grade = row[gradeCol].ToString().Trim();

        //                if (string.IsNullOrEmpty(studentID) ||
        //                    string.IsNullOrEmpty(teacherID) ||
        //                    string.IsNullOrEmpty(courseCode))
        //                    continue;

        //                // 🟢 NEW CHANGE: Duplicate check mein Section ko bhi shamil kar sakte hain agar zaruri ho
        //                bool exists = db.Enrollment.Any(e =>
        //                    e.studentID == studentID &&
        //                    e.courseCode == courseCode &&
        //                    e.sessionID == sessionId
        //                );

        //                if (exists)
        //                {
        //                    skippedDuplicate++;
        //                    continue;
        //                }

        //                // 🟢 NEW CHANGE: Enrollment table mein Section aur Grade save karna
        //                db.Enrollment.Add(new Enrollment
        //                {
        //                    studentID = studentID,
        //                    teacherID = teacherID,
        //                    courseCode = courseCode,
        //                    sessionID = sessionId,
        //                    Section = section, // Database mein Section save ho raha hai
        //                    Grade = grade      // Database mein Grade save ho raha hai
        //                });

        //                inserted++;
        //            }

        //            db.SaveChanges();
        //        }

        //        return Ok($"{inserted} enrollment uploaded successfully. {skippedDuplicate} duplicates skipped.");
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(ex);
        //    }
        //}

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

                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                int inserted = 0;
                int updated = 0;

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

                    // ✅ Column loop se bahar — ek baar dhundo
                    var sectionCol = table.Columns.Cast<DataColumn>()
                        .FirstOrDefault(c => c.ColumnName.Equals("Section", StringComparison.OrdinalIgnoreCase));
                    var gradeCol = table.Columns.Cast<DataColumn>()
                        .FirstOrDefault(c => c.ColumnName.Equals("Grade", StringComparison.OrdinalIgnoreCase));

                    foreach (DataRow row in table.Rows)
                    {
                        string studentID = row["studentID"]?.ToString().Trim();
                        string teacherID = row["teacherID"]?.ToString().Trim();
                        string courseCode = row["courseCode"]?.ToString().Trim();

                        string section = (sectionCol != null && row[sectionCol] != DBNull.Value)
                            ? row[sectionCol].ToString().Trim() : null;

                        string grade = (gradeCol != null && row[gradeCol] != DBNull.Value)
                            ? row[gradeCol].ToString().Trim() : null;

                        if (string.IsNullOrEmpty(studentID) ||
                            string.IsNullOrEmpty(teacherID) ||
                            string.IsNullOrEmpty(courseCode))
                            continue;

                        var existingEnrollment = db.Enrollment.FirstOrDefault(e =>
                            e.studentID == studentID &&
                            e.courseCode == courseCode &&
                            e.sessionID == sessionId
                        );

                        if (existingEnrollment != null)
                        {
                            // ✅ Update Section aur Grade
                            existingEnrollment.teacherID = teacherID; // TeacherID bhi update karna chahte hain
                            existingEnrollment.courseCode = courseCode; // CourseCode bhi update karna chahte hain
                            existingEnrollment.Section = section;
                            existingEnrollment.Grade = grade;
                            updated++;
                            continue;
                        }

                        // ✅ Naya record insert karo
                        db.Enrollment.Add(new Enrollment
                        {
                            studentID = studentID,
                            teacherID = teacherID,
                            courseCode = courseCode,
                            sessionID = sessionId,
                            Section = section,
                            Grade = grade
                        });

                        inserted++;
                    }

                    db.SaveChanges();
                }

                return Ok($"{inserted} new enrollments added. {updated} existing records updated (Section & Grade).");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}
