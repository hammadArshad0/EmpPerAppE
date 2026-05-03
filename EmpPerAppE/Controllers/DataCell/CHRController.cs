using EmpPerAppE.Models;
using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http;

namespace EmpPerAppE.Controllers.DataCell
{
    [RoutePrefix("api/CHR")]
    public class CHRController : ApiController
    {
        fypempEntities db = new fypempEntities();

        // ── Score Calculator ─────────────────────────────────────
        private int CalculateScore(int? lateInMinutes, int? leftEarlyMinutes = null)
        {
            int total = (lateInMinutes ?? 0) + (leftEarlyMinutes ?? 0);
            if (total >= 10) return 0;
            if (total >= 6) return 3;
            if (total >= 1) return 4;
            return 5;
        }

        private string GetComputedStatus(int? lateInMinutes, int? leftEarlyMinutes, string originalStatus)
        {
            int total = (lateInMinutes ?? 0) + (leftEarlyMinutes ?? 0);
            if (total >= 10) return "Cancelled";
            return string.IsNullOrEmpty(originalStatus) ? "Present" : originalStatus;
        }



        // ── 1. Excel Upload ──────────────────────────────────────
        [HttpPost]
        [Route("upload")]
        public IHttpActionResult UploadCHR()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;
                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];
                System.Text.Encoding.RegisterProvider(
                    System.Text.CodePagesEncodingProvider.Instance);

                using (var stream = file.InputStream)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                        { UseHeaderRow = true }
                    });

                    DataTable dataTable = result.Tables[0];
                    int maxIdBefore = db.CHR.Any() ? db.CHR.Max(x => x.id) : 0;
                    int insertedCount = 0;

                    // ── Unique batch timestamp — har upload ka alag waqt ──
                    // Milliseconds nahi — pure seconds tak round karo
                    // Phir us batch ki ek fixed time banao
                    DateTime uploadTime = new DateTime(
                        DateTime.Now.Year,
                        DateTime.Now.Month,
                        DateTime.Now.Day,
                        DateTime.Now.Hour,
                        DateTime.Now.Minute,
                        DateTime.Now.Second,
                        0  // milliseconds = 0
                    );

                    // Agar same second mein dobara upload ho to 1 second aage karo
                    bool conflict = db.CHR.Any(x => x.ClassDate == uploadTime);
                    if (conflict)
                        uploadTime = uploadTime.AddSeconds(1);

                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (row[0] == DBNull.Value ||
                            string.IsNullOrWhiteSpace(row[0].ToString())) continue;

                        Func<string, string> getVal = (colName) =>
                        {
                            var col = dataTable.Columns.Cast<DataColumn>()
                                .FirstOrDefault(c => c.ColumnName.Trim()
                                    .Equals(colName, StringComparison.OrdinalIgnoreCase));
                            return (col != null && row[col] != DBNull.Value)
                                ? row[col].ToString().Trim() : "";
                        };

                        db.CHR.Add(new CHR
                        {
                            CourseCode = getVal("CourseCode"),
                            TeacherID = getVal("TeacherID"),
                            TeacherName = getVal("TeacherName"),
                            Discipline = getVal("Discipline"),
                            Venue = getVal("Venue"),
                            Status = getVal("Status"),
                            Remarks = getVal("Remarks"),
                            LateIn = int.TryParse(getVal("LateIn"), out int late) ? late : 0,
                            LeftEarly = int.TryParse(getVal("LeftEarly"), out int left) ? left : 0,
                            ClassDate = uploadTime  // sab rows ko same fixed time
                        });
                        insertedCount++;
                    }

                    db.SaveChanges();

                    return Ok(new
                    {
                        Message = "Successfully Uploaded",
                        Count = insertedCount,
                        BatchStartId = maxIdBefore + 1,
                        Date = uploadTime.ToString("dd MMM yyyy HH:mm:ss")
                    });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception("Error during upload: " + ex.Message));
            }
        }
        // ── 2. HOD Dashboard ─────────────────────────────────────
        [HttpGet]
        [Route("GetHODDashboard")]
        public IHttpActionResult GetHODDashboard()
        {
            try
            {
                var allRows = db.CHR.OrderBy(c => c.id).ToList();
                if (!allRows.Any()) return Ok(new List<object>());

                var batches = allRows
                    .GroupBy(c => c.ClassDate)
                    .Select(g =>
                    {
                        var rows  = g.OrderBy(x => x.id).ToList();
                        var first = rows.First();
                        return new
                        {
                            ReportId         = first.id,
                            ReportDate       = first.ClassDate,
                            Department       = first.Discipline ?? "N/A",
                            TotalClasses     = rows.Count,
                            LateTeachers     = rows.Count(x => (x.LateIn    ?? 0) > 0),
                            EarlyLeavers     = rows.Count(x => (x.LeftEarly ?? 0) > 0),
                            CancelledClasses = rows.Count(x => (x.LateIn    ?? 0) >= 10),
                            AvgScore         = rows.Average(x =>
                                                (double)CalculateScore(x.LateIn))
                        };
                    })
                    .OrderByDescending(x => x.ReportId)
                    .ToList();

                return Ok(batches);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error: " + ex.Message));
            }
        }

        // ── 3. Detail by ReportId ────────────────────────────────
        [HttpGet]
        [Route("GetReportById/{id:int}")]
        public IHttpActionResult GetReportById(int id)
        {
            try
            {
                var anchor = db.CHR.FirstOrDefault(c => c.id == id);
                if (anchor == null) return NotFound();

                DateTime? batchDate = anchor.ClassDate;

                var data = db.CHR
                    .Where(c => c.ClassDate == batchDate)
                    .OrderBy(c => c.id)
                    .ToList()
                    .Select(c => new
                    {
                        c.id,
                        c.TeacherID,
                        c.TeacherName,
                        c.CourseCode,
                        c.Discipline,
                        c.Venue,
                        c.ClassDate,
                        LateIn    = c.LateIn    ?? 0,
                        LeftEarly = c.LeftEarly ?? 0,
                        c.Remarks,
                        Score = CalculateScore(c.LateIn, c.LeftEarly),
                        Status = GetComputedStatus(c.LateIn, c.LeftEarly, c.Status)
                    })
                    .ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception("Backend Error: " + ex.Message));
            }
        }

        // ── 4. Edit Row ──────────────────────────────────────────
        [HttpPut]
        [Route("EditRow/{id:int}")]
        public IHttpActionResult EditRow(int id, [FromBody] EditRowDto dto)
        {
            try
            {
                var row = db.CHR.FirstOrDefault(c => c.id == id);
                if (row == null) return NotFound();

                row.Remarks   = dto.Remarks;
                row.LateIn    = dto.LateIn;
                row.LeftEarly = dto.LeftEarly;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Updated",
                    Score = CalculateScore(row.LateIn, row.LeftEarly),
                    Status = GetComputedStatus(row.LateIn, row.LeftEarly, row.Status)
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception("Edit Error: " + ex.Message));
            }
        }

        // ── 5. Delete Row ────────────────────────────────────────
        [HttpDelete]
        [Route("DeleteRow/{id:int}")]
        public IHttpActionResult DeleteRow(int id)
        {
            try
            {
                var row = db.CHR.FirstOrDefault(c => c.id == id);
                if (row == null) return NotFound();

                db.CHR.Remove(row);
                db.SaveChanges();
                return Ok(new { Message = "Deleted", DeletedId = id });
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception("Delete Error: " + ex.Message));
            }
        }

        // ── 6. Delete Batch ──────────────────────────────────────
        [HttpDelete]
        [Route("DeleteBatch/{reportId:int}")]
        public IHttpActionResult DeleteBatch(int reportId)
        {
            try
            {
                var anchor = db.CHR.FirstOrDefault(c => c.id == reportId);
                if (anchor == null) return NotFound();

                DateTime? batchDate = anchor.ClassDate;
                var batch = db.CHR.Where(c => c.ClassDate == batchDate).ToList();

                db.CHR.RemoveRange(batch);
                db.SaveChanges();
                return Ok(new { Message = "Batch Deleted", Count = batch.Count });
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception("Delete Batch Error: " + ex.Message));
            }
        }

        // ── 7. Teacher Report ────────────────────────────────────
        [HttpGet]
        [Route("GetTeacherReport")]
        public IHttpActionResult GetTeacherReport(string teacherId)
        {
            try
            {
                if (string.IsNullOrEmpty(teacherId))
                    return BadRequest("Teacher ID required.");

                var data = db.CHR
                    .Where(c => c.TeacherID == teacherId)
                    .OrderByDescending(c => c.id)
                    .ToList()
                    .Select(c => new
                    {
                        c.id,
                        c.TeacherName,
                        c.CourseCode,
                        c.Discipline,
                        c.Venue,
                        c.ClassDate,
                        LateIn    = c.LateIn    ?? 0,
                        LeftEarly = c.LeftEarly ?? 0,
                        c.Remarks,
                        Score = CalculateScore(c.LateIn, c.LeftEarly),
                        Status = GetComputedStatus(c.LateIn, c.LeftEarly, c.Status)
                    })
                    .ToList();

                if (!data.Any()) return NotFound();
                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception("Backend Error: " + ex.Message));
            }
        }
    }

    // ── DTO ───────────────────────────────────────────────────────
    public class EditRowDto
    {
        public string Remarks   { get; set; }
        public int    LateIn    { get; set; }
        public int    LeftEarly { get; set; }
    }
}