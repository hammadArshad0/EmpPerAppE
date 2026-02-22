using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/CourseManagement")]
    public class CourseManagementController : ApiController
    {
        fypempEntities db = new fypempEntities();

        [HttpGet]
        [Route("EnrollmentCourses/{sessionId}")]
        public IHttpActionResult GetEnrollmentCourses(int sessionId)
        {
            try
            {
                // GroupBy use kar rahe hain taake Teacher aur Course ka unique combination milay
                var data = (from e in db.Enrollment
                            join t in db.Teacher on e.teacherID equals t.userID
                            join c in db.Course on e.courseCode equals c.code
                            where e.sessionID == sessionId
                            select new
                            {
                                teacherID = e.teacherID,
                                teacherName = t.name,
                                courseCode = e.courseCode,
                                courseTitle = c.title
                            })
                            .Distinct() // Yeh repeat entries ko khatam kar dega
                            .ToList();

                if (data.Count == 0)
                {
                    return Content(HttpStatusCode.NotFound, "No allocations found for this session.");
                }

                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}