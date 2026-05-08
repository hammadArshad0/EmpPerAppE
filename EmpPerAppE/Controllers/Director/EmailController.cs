
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Helpers;
using System.Web.Http;
using EmpPerAppE.Models;
using MailKit;
using MailKit.Search;
using EmpPerAppE.Models.DTO;

namespace EmpPerAppE.Controllers.Director
{
    [RoutePrefix("api/email")]
    public class EmailController : ApiController
    {
        fypempEntities db = new fypempEntities();

        // 1. Get All Emails - List dikhane ke liye
        //[HttpGet]
        //[Route("getall")]
        //public IHttpActionResult GetAllEmails()
        //{
        //    var emails = db.Email.OrderByDescending(x => x.id).ToList();
        //    return Ok(emails);
        //}
        [HttpGet]
        [Route("getall")]
        public IHttpActionResult GetAllEmails()
        {
            var emails = db.Email
                .Select(x => new
                {
                    x.id,
                    x.mail,
                    x.isActive
                })
                .OrderByDescending(x => x.id)
                .ToList();

            return Ok(emails);
        }

        // 2. Add New Email - Nayi email save karne ke liye
        //[HttpPost]
        //[Route("add")]
        //public IHttpActionResult AddEmail(Email model)
        //{
        //    if (model == null || string.IsNullOrEmpty(model.mail))
        //        return BadRequest("Email address is required.");

        //    model.isActive = false; // By default inactive rakhein
        //    db.Email.Add(model);
        //    db.SaveChanges();

        //    return Ok(new { message = "Email added successfully", data = model });
        //}

        [HttpPost]
        [Route("add")]
        public IHttpActionResult AddEmail(Email model)
        {
            if (model == null || string.IsNullOrEmpty(model.mail) || string.IsNullOrEmpty(model.password))
                return BadRequest("Email and App Password are required.");

            model.isActive = false;

            db.Email.Add(model);
            db.SaveChanges();

            return Ok(model);
        }


        // 3. Activate Email (Toggle Logic) - Aik ko active karein baaki sab ko inactive
        [HttpPut]
        [Route("activate/{id}")]
        public IHttpActionResult ActivateEmail(int id)
        {
            var allEmails = db.Email.ToList();
            var targetEmail = allEmails.FirstOrDefault(x => x.id == id);

            if (targetEmail == null)
                return NotFound();

            // Loop chala kar sab ko 0 aur selected ko 1 kar dein
            foreach (var email in allEmails)
            {
                if (email.id == id)
                    email.isActive = true;
                else
                    email.isActive = false;
            }

            db.SaveChanges();
            return Ok("Email activated. Others have been deactivated.");
        }

        // 4. Delete Email
        [HttpDelete]
        [Route("delete/{id}")]
        public IHttpActionResult DeleteEmail(int id)
        {
            var email = db.Email.Find(id);
            if (email == null) return NotFound();

            db.Email.Remove(email);
            db.SaveChanges();

            return Ok("Email deleted successfully.");
        }

        // 5. Get Current Active Recipient (For Frontend Banner)
        [HttpGet]
        [Route("active")]
        public IHttpActionResult GetActiveEmail()
        {
            var active = db.Email.FirstOrDefault(x => x.isActive == true);
            if (active == null) return NotFound();
            return Ok(active);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }


        // ============================================
        // 6. Update Email - Purani email ko edit karne ke liye
        // ============================================
        [HttpPut]
        [Route("edit/{id}")]
        public IHttpActionResult UpdateEmail(int id, Email model)
        {
            if (model == null || string.IsNullOrEmpty(model.mail))
                return BadRequest("Email address is required.");

            var emailToUpdate = db.Email.Find(id);

            if (emailToUpdate == null)
                return NotFound();

            // Sirf mail column ko update karein
            emailToUpdate.mail = model.mail;

            // Agar aap chahte hain ke edit ke waqt status change na ho to sirf mail save karein
            db.Entry(emailToUpdate).State = System.Data.Entity.EntityState.Modified;
            db.SaveChanges();

            return Ok(new { message = "Email updated successfully", data = emailToUpdate });
        }




        //[HttpPost]
        //[Route("get-evaluations")]
        //public IHttpActionResult GetEvaluations([FromBody] Email request)
        //{
        //    try
        //    {
        //        var emailRecord = db.Email.FirstOrDefault(e => e.mail == request.mail);
        //        if (emailRecord == null)
        //            return BadRequest("Email not found");

        //        var evaluations = new List<object>();

        //        using (var client = new MailKit.Net.Imap.ImapClient())
        //        {
        //            // ✅ SSL certificate ignore karo (common error)
        //            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

        //            client.Connect("imap.gmail.com", 993, true);
        //            client.Authenticate(emailRecord.mail, emailRecord.password);

        //            var inbox = client.Inbox;
        //            // 🔥 Dynamic filter
        //            SearchQuery query;

        //            if (request.filter == "unread")
        //            {
        //                query = SearchQuery.NotSeen;
        //            }
        //            else if (request.filter == "read")
        //            {
        //                query = SearchQuery.Seen;
        //            }
        //            else
        //            {
        //                query = SearchQuery.All;
        //            }

        //            inbox.Open(MailKit.FolderAccess.ReadOnly);

        //            // ✅ Sirf last 50 emails dekho - saari nahi
        //            var allUids = inbox.Search(MailKit.Search.SearchQuery.All);
        //            var recentUids = allUids.Reverse().Take(50).ToList();

        //            foreach (var uid in recentUids)
        //            {
        //                var message = inbox.GetMessage(uid);
        //                var body = message.TextBody ?? message.HtmlBody;

        //                if (string.IsNullOrEmpty(body)) continue;

        //                var start = body.IndexOf("START_EVAL");
        //                var end = body.IndexOf("END_EVAL");

        //                if (start == -1 || end == -1 || end <= start) continue;

        //                try
        //                {
        //                    var json = body.Substring(start + 10, end - (start + 10)).Trim();
        //                    var parsed = Newtonsoft.Json.Linq.JObject.Parse(json);

        //                    var studentNode = parsed["Student"] ?? parsed["student"];
        //                    var teacherEvalsNode = parsed["TeacherEvaluations"] ?? parsed["teacherEvaluations"];

        //                    if (teacherEvalsNode == null) continue;

        //                    foreach (var te in teacherEvalsNode)
        //                    {
        //                        var teacher = te["Teacher"] ?? te["teacher"];
        //                        var course = te["Course"] ?? te["course"];
        //                        var evals = te["Evaluations"] ?? te["evaluations"];

        //                        evaluations.Add(new
        //                        {
        //                            studentId = studentNode?["ID"]?.ToString() ?? studentNode?["id"]?.ToString() ?? "N/A",
        //                            studentName = studentNode?["Name"]?.ToString() ?? studentNode?["name"]?.ToString() ?? "N/A",
        //                            teacherId = teacher?["ID"]?.ToString() ?? teacher?["id"]?.ToString() ?? "N/A",
        //                            teacherName = teacher?["Name"]?.ToString() ?? teacher?["name"]?.ToString() ?? "N/A",
        //                            session = parsed["Session"]?.ToString() ?? parsed["session"]?.ToString() ?? "N/A",
        //                            subjectCode = course?["Code"]?.ToString() ?? course?["code"]?.ToString() ?? "N/A",
        //                            subjectTitle = course?["Title"]?.ToString() ?? course?["title"]?.ToString() ?? "N/A",
        //                            submittedOn = message.Date.DateTime,
        //                            evaluation = evals
        //                        });
        //                    }
        //                }
        //                catch (Exception parseEx)
        //                {
        //                    System.Diagnostics.Debug.WriteLine("PARSE ERROR: " + parseEx.Message);
        //                    continue;
        //                }
        //            }

        //            client.Disconnect(true);
        //        }

        //        return Ok(new
        //        {
        //            success = true,
        //            count = evaluations.Count,
        //            data = evaluations
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        // ✅ Full error message return karo
        //        return Ok(new
        //        {
        //            success = false,
        //            error = ex.Message,
        //            detail = ex.InnerException?.Message ?? "No inner exception"
        //        });
        //    }

        [HttpPost]
        [Route("get-evaluations")]
        public IHttpActionResult GetEvaluations([FromBody] EmailRequestDTO request)
        {
            // ✅ STEP 1: Request null check
            if (request == null)
                return Ok(new { success = false, error = "Request body is null. Check Content-Type header." });

            if (string.IsNullOrEmpty(request.mail))
                return Ok(new { success = false, error = "mail field is empty in request body." });

            try
            {
                // ✅ STEP 2: DB check
                var emailRecord = db.Email.FirstOrDefault(e => e.mail == request.mail);
                if (emailRecord == null)
                    return Ok(new { success = false, error = $"Email not found in DB: {request.mail}" });

                if (string.IsNullOrEmpty(emailRecord.password))
                    return Ok(new { success = false, error = "Email password is empty in database." });

                var evaluations = new List<object>();
                string filterVal = (request.filter ?? "all").Trim().ToLower();

                using (var client = new MailKit.Net.Imap.ImapClient())
                {
                    // ✅ STEP 3: SSL ignore
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    // ✅ STEP 4: Connect
                    try
                    {
                        client.Connect("imap.gmail.com", 993, true);
                    }
                    catch (Exception connEx)
                    {
                        return Ok(new { success = false, error = "IMAP Connect failed: " + connEx.Message });
                    }

                    // ✅ STEP 5: Authenticate
                    try
                    {
                        client.Authenticate(emailRecord.mail, emailRecord.password);
                    }
                    catch (Exception authEx)
                    {
                        return Ok(new
                        {
                            success = false,
                            error = "Gmail Authentication failed. App Password use karein (normal password kaam nahi karta): " + authEx.Message
                        });
                    }

                    var inbox = client.Inbox;
                    inbox.Open(MailKit.FolderAccess.ReadWrite);

                    // ✅ STEP 6: Filter apply
                    SearchQuery query;
                    if (filterVal == "unread")
                        query = SearchQuery.NotSeen;
                    else if (filterVal == "read")
                        query = SearchQuery.Seen;
                    else
                        query = SearchQuery.All;

                    // ✅ STEP 7: Search
                    IList<UniqueId> filteredUids;
                    try
                    {
                        filteredUids = inbox.Search(query);
                    }
                    catch (Exception searchEx)
                    {
                        client.Disconnect(true);
                        return Ok(new { success = false, error = "Search failed: " + searchEx.Message });
                    }

                    if (filteredUids == null || filteredUids.Count == 0)
                    {
                        client.Disconnect(true);
                        return Ok(new
                        {
                            success = true,
                            count = 0,
                            data = evaluations,
                            debug = $"Filter '{filterVal}' applied. No emails found matching this filter."
                        });
                    }

                    var recentUids = filteredUids.Reverse().Take(50).ToList();

                    // ✅ STEP 8: Fetch messages one by one (batch fetch remove kiya - compatibility issue)
                    foreach (var uid in recentUids)
                    {
                        MimeKit.MimeMessage message;
                        try
                        {
                            message = inbox.GetMessage(uid);
                        }
                        catch { continue; }

                        // Unread ko read mark karo
                        if (filterVal == "unread")
                        {
                            try { inbox.AddFlags(uid, MessageFlags.Seen, true); }
                            catch { /* ignore */ }
                        }

                        // Body nikalo
                        string body = message.TextBody;

                        // Agar TextBody nahi mila to HtmlBody se tags strip karo
                        if (string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(message.HtmlBody))
                        {
                            body = System.Text.RegularExpressions.Regex.Replace(
                                message.HtmlBody, "<.*?>", string.Empty
                            );
                            // HTML entities decode karo
                            body = System.Web.HttpUtility.HtmlDecode(body);
                        }

                        if (string.IsNullOrWhiteSpace(body)) continue;

                        var start = body.IndexOf("START_EVAL");
                        var end = body.IndexOf("END_EVAL");

                        if (start == -1 || end == -1 || end <= start) continue;

                        try
                        {
                            var json = body.Substring(start + 10, end - (start + 10)).Trim();
                            var parsed = Newtonsoft.Json.Linq.JObject.Parse(json);

                            var studentNode = parsed["Student"] ?? parsed["student"];
                            var teacherEvalsNode = parsed["TeacherEvaluations"] ?? parsed["teacherEvaluations"];

                            if (teacherEvalsNode == null) continue;

                            foreach (var te in teacherEvalsNode)
                            {
                                var teacher = te["Teacher"] ?? te["teacher"];
                                var course = te["Course"] ?? te["course"];
                                var evals = te["Evaluations"] ?? te["evaluations"];

                                evaluations.Add(new
                                {
                                    studentId = studentNode?["ID"]?.ToString() ?? studentNode?["id"]?.ToString() ?? "N/A",
                                    studentName = studentNode?["Name"]?.ToString() ?? studentNode?["name"]?.ToString() ?? "N/A",
                                    teacherId = teacher?["ID"]?.ToString() ?? teacher?["id"]?.ToString() ?? "N/A",
                                    teacherName = teacher?["Name"]?.ToString() ?? teacher?["name"]?.ToString() ?? "N/A",
                                    session = parsed["Session"]?.ToString() ?? parsed["session"]?.ToString() ?? "N/A",
                                    subjectCode = course?["Code"]?.ToString() ?? course?["code"]?.ToString() ?? "N/A",
                                    subjectTitle = course?["Title"]?.ToString() ?? course?["title"]?.ToString() ?? "N/A",
                                    submittedOn = message.Date.DateTime,
                                    evaluation = evals
                                });
                            }
                        }
                        catch (Exception parseEx)
                        {
                            System.Diagnostics.Debug.WriteLine("PARSE ERROR: " + parseEx.Message);
                            continue;
                        }
                    }

                    client.Disconnect(true);
                }

                return Ok(new
                {
                    success = true,
                    count = evaluations.Count,
                    data = evaluations
                });
            }
            catch (Exception ex)
            {
                // ✅ Full error details return karo
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    detail = ex.InnerException?.Message ?? "No inner exception",
                    stackHint = ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? "No stack"
                });
            }
        }
    }
    }

        