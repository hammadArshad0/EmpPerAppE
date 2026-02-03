using EmpPerAppE.Models;
using EmpPerAppE.Models.kpi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Transactions;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/kpi")]
    public class KpiController : ApiController
    {
        fypempEntities db = new fypempEntities();

        [HttpPost]
        [Route("create-with-weight")]
        public IHttpActionResult CreateWithWeight(AddKPIDto dto)
        {
            if (dto == null || dto.SubKPIs == null || dto.SubKPIs.Count == 0)
                return BadRequest("Data incomplete.");

            try
            {
                using (var scope = new TransactionScope())
                {
                    // 1. Create Main KPI (Agar pehle se nahi hai)
                    KPI kpi = new KPI { name = dto.KPIName, KPI_Employeetype = dto.EmployeeTypeId };
                    db.KPI.Add(kpi);
                    db.SaveChanges();

                    // 2. Add Sub-KPIs
                    foreach (var subDto in dto.SubKPIs)
                    {
                        var subObj = new SubKPI { KPIID = kpi.id, name = subDto.Name };
                        db.SubKPI.Add(subObj);
                        db.SaveChanges(); // ID generate karne ke liye

                        // Initial weight save karein (Backend ise adjust karega niche)
                        db.SessionKPIWeight.Add(new SessionKPIWeight
                        {
                            SessionID = dto.SessionId,
                            KPIID = kpi.id,
                            SubKPIID = subObj.id,
                            Weight = (int)subDto.Weight // Initial input
                        });
                    }
                    db.SaveChanges();

                    // 3. GLOBAL ADJUSTMENT (The 100% Rule for Category)
                    // Is Category (EmployeeType) ke saare KPIs nikaalein jo is Session mein hain
                    var allWeightsInCategory = db.SessionKPIWeight
                        .Where(w => w.SessionID == dto.SessionId &&
                                    db.KPI.Any(k => k.id == w.KPIID && k.KPI_Employeetype == dto.EmployeeTypeId))
                        .ToList();

                    decimal currentGrandTotal = allWeightsInCategory.Sum(w => (decimal)(w.Weight ?? 0));

                    if (currentGrandTotal > 0)
                    {
                        // Factor calculation: Target 100 / Jo abhi total hai
                        decimal factor = 100m / currentGrandTotal;

                        foreach (var w in allWeightsInCategory)
                        {
                            decimal adjusted = (decimal)(w.Weight ?? 0) * factor;
                            // Rounding away from zero to keep it clean
                            w.Weight = (int)Math.Round(adjusted, MidpointRounding.AwayFromZero);
                        }
                        db.SaveChanges();
                    }

                    // 4. FINAL CHECK (Rounding Error Fix)
                    // Kabhi kabhi rounding ki wajah se total 101 ya 99 ho jata hai. 
                    // Hum aakhri element mein difference adjust kar dete hain.
                    var finalWeights = db.SessionKPIWeight
                        .Where(w => w.SessionID == dto.SessionId &&
                                    db.KPI.Any(k => k.id == w.KPIID && k.KPI_Employeetype == dto.EmployeeTypeId))
                        .ToList();

                    int finalSum = finalWeights.Sum(w => w.Weight ?? 0);
                    if (finalSum != 100 && finalWeights.Count > 0)
                    {
                        int diff = 100 - finalSum;
                        finalWeights.First().Weight += diff; // Pehle item mein adjustment add/sub kardein
                        db.SaveChanges();
                    }

                    scope.Complete();
                    return Ok(new { Message = "KPI Saved and Weights Adjusted to exactly 100%.", Status = "Success" });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Adjustment Failed: " + ex.Message));
            }
        }

        [HttpGet]
        [Route("view-weights/{sid}/{eid}")]
        public IHttpActionResult GetWeights(int sid, int eid)
        {
            try
            {
                var res = db.KPI
                    .Where(k => k.KPI_Employeetype == eid)
                    .ToList() // Memory mein laa kar mapping karein
                    .Select(k => new {
                        kpiId = k.id,
                        kpiName = k.name,
                        // Is KPI ke andar jitne sub-kpis hain unka total weight calculate karein
                        totalKpiWeight = db.SessionKPIWeight
                                         .Where(w => w.SessionID == sid && w.KPIID == k.id)
                                         .Sum(w => (int?)w.Weight) ?? 0,

                        subKpis = (from w in db.SessionKPIWeight
                                   join s in db.SubKPI on w.SubKPIID equals s.id
                                   where w.SessionID == sid && w.KPIID == k.id
                                   select new
                                   {
                                       subKpiId = s.id,
                                       subKpiName = s.name,
                                       weight = w.Weight
                                   }).ToList()
                    })
                    .Where(x => x.totalKpiWeight > 0) // Sirf wo dikhayein jinka weight set hai
                    .ToList();

                return Ok(res);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
        [HttpGet]
        [Route("sessions")]
        public IHttpActionResult GetSessions() => Ok(db.Session.Select(s => new { s.id, s.name }).ToList());

        [HttpGet]
        [Route("emptypes")]
        public IHttpActionResult GetEmpTypes() => Ok(db.EmployeeType.Select(e => new { e.id, e.type }).ToList());
    }
}