using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using EmpPerAppE.Models;
using ExcelDataReader;
using static System.Net.WebRequestMethods;

namespace EmpPerAppE.Controllers.student
{
    [RoutePrefix("api/student")]
    public class StudentController : ApiController
    {
        fypempEntities db = new fypempEntities();
        // STUDENT API
     



        //[HttpPost]
        //[Route("upload")]
        //public IHttpActionResult UploadStudent()
        //{
        //    try
        //    {
        //        var httpRequest = HttpContext.Current.Request;

        //        if (httpRequest.Files.Count == 0)
        //            return BadRequest("No file uploaded.");

        //        var file = httpRequest.Files[0];
        //        if (file == null || file.ContentLength == 0)
        //            return BadRequest("Empty file.");

        //        // Needed for ExcelDataReader
        //        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        //        using (var stream = file.InputStream)
        //        using (var reader = ExcelReaderFactory.CreateReader(stream))
        //        {
        //            var result = reader.AsDataSet(new ExcelDataSetConfiguration()
        //            {
        //                ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
        //            });

        //            var dataTable = result.Tables[0];

        //            int insertedCount = 0;
        //            int skippedInvalidSession = 0;
        //            int skippedDuplicate = 0;

        //            foreach (DataRow row in dataTable.Rows)
        //            {
        //                // Validate required columns
        //                if (row["UserID"] == DBNull.Value || row["Name"] == DBNull.Value || row["AdmissionSessionId"] == DBNull.Value)
        //                    continue;

        //                string userId = row["UserID"].ToString().Trim();
        //                string name = row["Name"].ToString().Trim();
        //                int sessionId;

        //                // Try parse AdmissionSessionId
        //                if (!int.TryParse(row["AdmissionSessionId"].ToString(), out sessionId))
        //                    continue;

        //                // Check if session exists
        //                if (db.Session.Find(sessionId) == null)
        //                {
        //                    skippedInvalidSession++;
        //                    continue;
        //                }

        //                // 1️⃣ Check if user exists in Users table
        //                var existingUser = db.Users.Find(userId);

        //                if (existingUser == null)
        //                {
        //                    // 2️⃣ User doesn't exist, add to Users first
        //                    Users newUser = new Users
        //                    {
        //                        id = userId,
        //                        password = "default123",       // Can be customized
        //                        role = "Student",
        //                        profileImagePath = null,
        //                        isActive = 1
        //                    };
        //                    db.Users.Add(newUser);
        //                }

        //                // 3️⃣ Check if student already exists in Student table
        //                if (db.Student.Find(userId) != null)
        //                {
        //                    skippedDuplicate++;
        //                    continue;
        //                }

        //                // 4️⃣ Add to Student table
        //                db.Student.Add(new EmpPerAppE.Models.Student
        //                {
        //                    userID = userId,
        //                    name = name,
        //                    admissionSessionID = sessionId
        //                });

        //                insertedCount++;
        //            }

        //            // Save all changes in one transaction (Users + Student)
        //            db.SaveChanges();

        //            string message = $"{insertedCount} students uploaded successfully.";
        //            if (skippedDuplicate > 0) message += $" {skippedDuplicate} duplicate(s) skipped.";
        //            if (skippedInvalidSession > 0) message += $" {skippedInvalidSession} row(s) skipped due to invalid session ID.";

        //            return Ok(message);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(ex);
        //    }
        //}




        //get all students

        [HttpGet]
        [Route("studentlist")]
        public IHttpActionResult GetAllStudents()
        {
            try
            {
                var students = (from s in db.Student
                                join se in db.Session
                                on s.admissionSessionID equals se.id
                                select new
                                {
                                    UserID = s.userID,
                                    StudentName = s.name,
                                    AdmissionSessionID = se.id,
                                    AdmissionSessionName = se.name
                                }).ToList();

                return Ok(students);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
        [HttpPost]
        [Route("upload")]
        public IHttpActionResult UploadStudent()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                // 1️⃣ Read sessionId from form
                if (string.IsNullOrEmpty(httpRequest.Form["sessionId"]))
                    return BadRequest("Session not selected.");

                int sessionId = int.Parse(httpRequest.Form["sessionId"]);

                // 2️⃣ Check session exists
                if (db.Session.Find(sessionId) == null)
                    return BadRequest("Invalid session selected.");

                // 3️⃣ Check file uploaded
                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];
                if (file == null || file.ContentLength == 0)
                    return BadRequest("Empty file.");

                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

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

                    var dataTable = result.Tables[0];

                    int inserted = 0;
                    int skippedDuplicate = 0;

                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (row["UserID"] == DBNull.Value || row["Name"] == DBNull.Value)
                            continue;

                        string userId = row["UserID"].ToString().Trim();
                        string name = row["Name"].ToString().Trim();

                        // ✅ FIXED CGPA reading
                        decimal cgpa = 0;
                        var cgpaCol = dataTable.Columns.Cast<DataColumn>()
                            .FirstOrDefault(c => c.ColumnName.Equals("CGPA", StringComparison.OrdinalIgnoreCase));

                        if (cgpaCol != null && row[cgpaCol] != DBNull.Value)
                        {
                            decimal.TryParse(row[cgpaCol].ToString().Trim(), out cgpa);
                        }

                        // Users table
                        if (db.Users.Find(userId) == null)
                        {
                            db.Users.Add(new Users
                            {
                                id = userId,
                                password = "123",
                                role = "Student",
                                isActive = 1
                            });
                        }
                        var existingStudent = db.Student.Find(userId);
                        if (existingStudent != null)
                        {
                            // ✅ Sirf CGPA update karo
                            existingStudent.CGPA = cgpa;
                            skippedDuplicate++;
                            continue;
                        }


                        // if (db.Student.Find(userId) != null)
                        //{
                        //    skippedDuplicate++;
                        //    continue;
                        //}

                        db.Student.Add(new Student
                        {
                            userID = userId,
                            name = name,
                            admissionSessionID = sessionId,
                            CGPA = cgpa  // ✅ Ab sahi value jayegi
                        });

                        inserted++;
                    }
                    // Student duplicate check
                   
                    db.SaveChanges();

                    return Ok($"{inserted} students uploaded. {skippedDuplicate} duplicates skipped.");
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }



    }
}

