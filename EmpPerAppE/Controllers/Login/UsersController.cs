//using EmpPerAppE.Models;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Web.Http;

//namespace EmpPerAppE.Controllers.Login
//{
//    [RoutePrefix("api/Users")]
//    public class UsersController : ApiController
//    {
//        fypempEntities db = new fypempEntities();


//        [Route("Login")]
//            [HttpPost]
//            public HttpResponseMessage Login(string id, string password)
//            {
//                var res = db.Users.FirstOrDefault(x => x.id == id && x.password == password && x.isActive == 1);
//                if (res == null)
//                {
//                    return Request.CreateResponse(HttpStatusCode.NotFound, "No User Found");

//                }

//                return Request.CreateResponse(HttpStatusCode.OK, new { message = "LoginSuccessful", role = res.role , userId = res.id });

//            }
//        }
//}
using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EmpPerAppE.Controllers.Login
{
    [RoutePrefix("api/Users")]
    public class UsersController : ApiController
    {
        fypempEntities db = new fypempEntities();

        [Route("Login")]
        [HttpPost]
        public HttpResponseMessage Login(string id, string password)
        {
            try
            {
                // 1. Authenticate User from Users Table
                var userRecord = db.Users.FirstOrDefault(x => x.id == id && x.password == password && x.isActive == 1);

                if (userRecord == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No User Found");
                }

                string fullName = userRecord.id;
                string currentSessionName = "N/A";
                int? currentSessionId = null;

                // 2. Role based Name and Session tracking using Enrollment Table
                if (userRecord.role == "Student")
                {
                    var student = db.Student.FirstOrDefault(s => s.userID == userRecord.id);
                    if (student != null)
                    {
                        fullName = student.name;

                        // Student ki latest enrollment se session nikalna
                        var enrollment = db.Enrollment
                                           .Where(e => e.studentID == student.userID)
                                           .OrderByDescending(e => e.id)
                                           .FirstOrDefault();

                        if (enrollment != null)
                        {
                            var session = db.Session.FirstOrDefault(s => s.id == enrollment.sessionID);
                            if (session != null)
                            {
                                currentSessionId = session.id;
                                currentSessionName = session.name;
                            }
                        }
                    }
                }
                else if (userRecord.role == "Teacher")
                {
                    var teacher = db.Teacher.FirstOrDefault(t => t.userID == userRecord.id);
                    if (teacher != null)
                    {
                        fullName = teacher.name;

                        // Teacher jis session mein students ko parha raha hai (Enrollment Table se)
                        var teacherRecord = db.Enrollment
                                           .Where(e => e.teacherID == teacher.userID)
                                           .OrderByDescending(e => e.id)
                                           .FirstOrDefault();

                        if (teacherRecord != null)
                        {
                            var session = db.Session.FirstOrDefault(s => s.id == teacherRecord.sessionID);
                            if (session != null)
                            {
                                currentSessionId = session.id;
                                currentSessionName = session.name;
                            }
                        }
                    }
                }
                else if (userRecord.role == "HOD" || userRecord.role == "Admin")
                {
                    fullName = userRecord.role == "HOD" ? "Department Head" : "Administrator";
                    // Management roles ke liye default latest session
                    var lastSession = db.Session.OrderByDescending(s => s.id).FirstOrDefault();
                    if (lastSession != null)
                    {
                        currentSessionId = lastSession.id;
                        currentSessionName = lastSession.name;
                    }
                }

                // 3. Complete Response for Integration
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "LoginSuccessful",
                    role = userRecord.role,
                    userId = userRecord.id,
                    name = fullName,
                    sessionName = currentSessionName,
                    sessionId = currentSessionId
                });
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }
}