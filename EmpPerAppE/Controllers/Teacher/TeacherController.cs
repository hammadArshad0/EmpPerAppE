using EmpPerAppE.Models;
using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;

namespace EmpPerAppE.Controllers
{
    [RoutePrefix("api/teacher")]
    public class TeacherController : ApiController
    {
        fypempEntities db = new fypempEntities();

        // POST api/teacher/upload
        [HttpPost]
        [Route("upload")]
        public IHttpActionResult Upload()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];

                if (file == null || file.ContentLength == 0)
                    return BadRequest("Empty file.");

                // Needed for ExcelDataReader
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

                    int insertedCount = 0;
                    int skippedDuplicate = 0;
                    int skippedInvalid = 0;

                    foreach (DataRow row in dataTable.Rows)
                    {
                        // ✅ Validate required columns
                        if (row["Userid"] == DBNull.Value ||
                            row["Name"] == DBNull.Value ||
                            row["Department"] == DBNull.Value)
                        {
                            skippedInvalid++;
                            continue;
                        }

                        string userId = row["Userid"].ToString().Trim();
                        string name = row["Name"].ToString().Trim();
                        string department = row["Department"].ToString().Trim();

                        // Skip empty values
                        if (string.IsNullOrWhiteSpace(userId) ||
                            string.IsNullOrWhiteSpace(name) ||
                            string.IsNullOrWhiteSpace(department))
                        {
                            skippedInvalid++;
                            continue;
                        }

                        // 1️⃣ Check if user exists in Users table
                        var existingUser = db.Users.Find(userId);

                        if (existingUser == null)
                        {
                            // 2️⃣ User doesn't exist, add to Users first
                            Users newUser = new Users
                            {
                                id = userId,
                                password = "default123",
                                role = "Teacher",
                                profileImagePath = null,
                                isActive = 1
                            };
                            db.Users.Add(newUser);
                        }

                        // 3️⃣ Check if teacher already exists in Teacher table
                        if (db.Teacher.Find(userId) != null)
                        {
                            skippedDuplicate++;
                            continue;
                        }

                        // 4️⃣ Add to Teacher table
                        Teacher teacher = new Teacher()
                        {
                            userID = userId,
                            name = name,
                            department = department
                        };

                        db.Teacher.Add(teacher);
                        insertedCount++;
                    }

                    // Save all changes (Users + Teacher) in one transaction
                    db.SaveChanges();

                    string message = $"{insertedCount} teachers uploaded successfully.";
                    if (skippedDuplicate > 0)
                        message += $" {skippedDuplicate} duplicate(s) skipped.";
                    if (skippedInvalid > 0)
                        message += $" {skippedInvalid} invalid row(s) skipped.";

                    return Ok(message);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // Get all teachers
        [HttpGet]
        [Route("Teacherlist")]
        public IHttpActionResult GetAllTeachers()
        {
            try
            {
                var teachers = db.Teacher
                    .Select(t => new
                    {
                        UserID = t.userID,
                        Name = t.name,
                        Department = t.department
                    })
                    .ToList();

                return Ok(teachers);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("ping")]
        public IHttpActionResult Ping()
        {
            return Ok("Teacher API is alive");
        }
    }
}