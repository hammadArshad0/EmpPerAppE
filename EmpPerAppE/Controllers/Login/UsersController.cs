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
                // 1. Authenticate User
                var userRecord = db.Users.FirstOrDefault(x => x.id == id && x.password == password && x.isActive == 1);

                if (userRecord == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No User Found");
                }

                string fullName = userRecord.id;
                string currentSessionName = "N/A";
                int? currentSessionId = null;

                // Sabse pehle Global Latest Session nikal lete hain fallback ke liye
                var globalLatestSession = db.Session.OrderByDescending(s => s.id).FirstOrDefault();

                // 2. Role based Name and Latest Session tracking
                if (userRecord.role == "Student")
                {
                    var student = db.Student.FirstOrDefault(s => s.userID == userRecord.id);
                    if (student != null)
                    {
                        fullName = student.name;
                        // Student ki sabse latest (Highest Session ID) enrollment uthana
                        var enrollment = db.Enrollment
                                           .Where(e => e.studentID == student.userID)
                                           .OrderByDescending(e => e.sessionID) // Yahan sessionID se sort kiya
                                           .FirstOrDefault();

                        var session = (enrollment != null)
                                      ? db.Session.FirstOrDefault(s => s.id == enrollment.sessionID)
                                      : globalLatestSession;

                        if (session != null)
                        {
                            currentSessionId = session.id;
                            currentSessionName = session.name;
                        }
                    }
                }
                else if (userRecord.role == "Teacher")
                {
                    var teacher = db.Teacher.FirstOrDefault(t => t.userID == userRecord.id);
                    if (teacher != null)
                    {
                        fullName = teacher.name;
                        // Teacher jis latest session mein parha raha hai
                        var teacherRecord = db.Enrollment
                                               .Where(e => e.teacherID == teacher.userID)
                                               .OrderByDescending(e => e.sessionID) // Yahan sessionID se sort kiya
                                               .FirstOrDefault();

                        var session = (teacherRecord != null)
                                      ? db.Session.FirstOrDefault(s => s.id == teacherRecord.sessionID)
                                      : globalLatestSession;

                        if (session != null)
                        {
                            currentSessionId = session.id;
                            currentSessionName = session.name;
                        }
                    }
                }
                else // HOD or Admin
                {
                    fullName = userRecord.role == "HOD" ? "Dr.Munir" : "Dr.Jamil Sawar";
                    if (globalLatestSession != null)
                    {
                        currentSessionId = globalLatestSession.id;
                        currentSessionName = globalLatestSession.name;
                    }
                }

                // 3. Final Response
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

