using EmpPerAppE.Models;
using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace EmpPerAppE.Controllers
{
    [RoutePrefix("api/teacher")]
    public class TeacherController : ApiController
    {
        EmpPerAppE.Models.fypempEntities db = new EmpPerAppE.Models.fypempEntities();

        [HttpPost]
        [Route("upload")]
        public IHttpActionResult Upload()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;
                if (httpRequest.Files.Count == 0) return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using (var stream = file.InputStream)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                    });

                    var dataTable = result.Tables[0];
                    int insertedCount = 0;
                    int skippedDuplicate = 0;
                    int skippedInvalid = 0;

                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (row["Userid"] == DBNull.Value || row["Name"] == DBNull.Value || row["Department"] == DBNull.Value)
                        {
                            skippedInvalid++;
                            continue;
                        }

                        string userId = row["Userid"].ToString().Trim();
                        string name = row["Name"].ToString().Trim();
                        string department = row["Department"].ToString().Trim();

                        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(name))
                        {
                            skippedInvalid++;
                            continue;
                        }

                        // 1. Check Users table
                        var existingUser = db.Users.Find(userId);
                        if (existingUser == null)
                        {
                            db.Users.Add(new EmpPerAppE.Models.Users
                            {
                                id = userId,
                                password = "default123",
                                role = "Teacher",
                                isActive = 1
                            });
                        }

                        // 2. Fix here: Use full path for Teacher Model
                        if (db.Teacher.Find(userId) != null)
                        {
                            skippedDuplicate++;
                            continue;
                        }

                        // ✅ FIX: EmpPerAppE.Models.Teacher use kiya taake namespace conflict na ho
                        EmpPerAppE.Models.Teacher teacherObj = new EmpPerAppE.Models.Teacher()
                        {
                            userID = userId,
                            name = name,
                            department = department
                        };

                        db.Teacher.Add(teacherObj);
                        insertedCount++;
                    }

                    db.SaveChanges();
                    return Ok($"{insertedCount} teachers uploaded successfully.");
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("Teacherlist")]
        public IHttpActionResult GetAllTeachers()
        {
            try
            {
                // Select query mein bhi db.Teacher check karein
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

            // --- SIMPLE UPDATE: Sirf ID aur query strings/params use karein ---
            [HttpPut]
            [Route("update")]
            public IHttpActionResult UpdateTeacher(string id, string name, string dept)
            {
                try
                {
                    var teacher = db.Teacher.Find(id);
                    if (teacher == null) return NotFound();

                    // Simple assignments
                    teacher.name = name;
                    teacher.department = dept;

                    db.SaveChanges();
                    return Ok("Teacher updated successfully.");
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }

            // --- SIMPLE DELETE: Sirf ID pass karein ---
            [HttpDelete]
            [Route("delete")]
            public IHttpActionResult DeleteTeacher(string id)
            {
                try
                {
                    var teacher = db.Teacher.Find(id);
                    if (teacher == null) return NotFound();

                    // 1. Pehle Teacher table se delete karein
                    db.Teacher.Remove(teacher);

                    // 2. Phir Users table se uska login delete karein
                    var userAcc = db.Users.Find(id);
                    if (userAcc != null) db.Users.Remove(userAcc);

                    db.SaveChanges();
                    return Ok("Teacher deleted successfully.");
                }
                catch (Exception ex)
                {
                    return BadRequest("Delete failed. ID might be linked elsewhere.");
                }
            }
        }
    }
