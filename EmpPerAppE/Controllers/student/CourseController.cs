using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Http;
using EmpPerAppE.Models;
using ExcelDataReader;
using System.Data.Entity;

namespace EmpPerAppE.Controllers.student
{
    [RoutePrefix("api/course")]
    public class CourseController : ApiController
    {
        private fypempEntities db = new fypempEntities();

        // 1. GET ALL COURSES (Schema: code, title)
        [HttpGet]
        [Route("courseslist")]
        public IHttpActionResult GetAllCourses()
        {
            try
            {
                var courses = db.Course
                    .Select(c => new
                    {
                        code = c.code,
                        title = c.title
                    })
                    .ToList();
                return Ok(courses);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 2. UPLOAD EXCEL
        [HttpPost]
        [Route("upload")]
        public IHttpActionResult UploadCourse()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;
                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using (var stream = file.InputStream)
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                        });

                        var dataTable = result.Tables[0];
                        int insertedCount = 0;
                        int skippedCount = 0;

                        foreach (DataRow row in dataTable.Rows)
                        {
                            // Check matching Excel headers with DB schema
                            if (row["code"] == DBNull.Value || row["title"] == DBNull.Value) continue;

                            string cCode = row["code"].ToString().Trim();
                            string cTitle = row["title"].ToString().Trim();

                            if (string.IsNullOrEmpty(cCode)) continue;

                            // Primary Key (code) check to avoid duplicates
                            var existing = db.Course.FirstOrDefault(c => c.code == cCode);
                            if (existing != null)
                            {
                                skippedCount++;
                                continue;
                            }

                            db.Course.Add(new Course { code = cCode, title = cTitle });
                            insertedCount++;
                        }

                        db.SaveChanges();
                        return Ok(new { Message = "Upload Successful", Saved = insertedCount, Skipped = skippedCount });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Upload Error: " + ex.Message));
            }
        }

        // 3. UPDATE COURSE (Using 'code' as path variable)
        [HttpPut]
        [Route("update/{code}")]
        public IHttpActionResult UpdateCourse(string code, [FromBody] Course updatedData)
        {
            try
            {
                var existing = db.Course.FirstOrDefault(c => c.code == code);
                if (existing == null) return NotFound();

                // Sirf Title update hoga kyunke 'code' Primary Key hai
                existing.title = updatedData.title;

                db.Entry(existing).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();

                return Ok("Course updated successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 4. DELETE COURSE (Using 'code')
        [HttpDelete]
        [Route("delete/{code}")]
        public IHttpActionResult DeleteCourse(string code)
        {
            try
            {
                // Find record by code string
                var course = db.Course.FirstOrDefault(c => c.code == code);
                if (course == null) return NotFound();

                db.Course.Remove(course);
                db.SaveChanges();
                return Ok("Course deleted successfully");
            }
            catch (Exception ex)
            {
                return BadRequest("Delete failed. Course is likely linked to other tables.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}