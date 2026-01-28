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
    [RoutePrefix("api/course")]

    public class CourseController : ApiController
    {
        fypempEntities db = new fypempEntities();
        // COURSE API
    
     
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
                    if (file == null || file.ContentLength == 0)
                        return BadRequest("Empty file.");

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

                        foreach (DataRow row in dataTable.Rows)
                        {
                            if (row["Code"] == DBNull.Value || row["Title"] == DBNull.Value) continue;

                            string code = row["Code"].ToString().Trim();
                            string title = row["Title"].ToString().Trim();

                            if (db.Course.Find(code) != null) continue;

                            db.Course.Add(new EmpPerAppE.Models.Course { code = code, title = title });
                            insertedCount++;
                        }

                        db.SaveChanges();
                        return Ok($"{insertedCount} courses uploaded successfully.");
                    }
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }
        //get all cousres

        [HttpGet]
        [Route("courseslist")]
        public IHttpActionResult GetAllCourses()
        {
            try
            {
                var courses = db.Course
                    .Select(c => new
                    {
                        CourseCode = c.code,
                        CourseTitle = c.title
                    })
                    .ToList();

                return Ok(courses);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


        [HttpGet]
            [Route("ping")]
            public IHttpActionResult PingCourse() => Ok("Course API is alive");
        }
    }
